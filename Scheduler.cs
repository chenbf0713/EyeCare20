using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace EyeCare20
{
    /// <summary>
    /// 调度器 —— 双模式计时，统一管理四类提醒（望远/眨眼/久坐/喝水）：
    /// 简单模式：按系统绝对时间循环；卡片关闭（自然完成或跳过）后才开始下一轮周期；
    /// 到期时间相差 5 分钟内的提醒向下合并为一次同时提醒；
    /// 重排下一轮时任意两个提醒的触发时刻至少相差 5 分钟（错峰）。
    /// 高级模式：仅操作电脑时累计时长，同刻到期的提醒合并触发。
    /// 同一时刻只显示一张卡片；全屏应用时抑制。
    /// </summary>
    internal sealed class Scheduler
    {
        private const double PendingTimeoutSeconds = 300;   // 卡片关闭回调保险超时
        private const double MergeWindowMinutes = 5.0;     // 到期相差 5 分钟内合并
        private const double MinSpacingMinutes = 5.0;      // 任意两提醒至少相差 5 分钟

        /// <summary>单个提醒计划：启用 + 间隔 + 各自的计时状态。</summary>
        private sealed class Plan
        {
            public ReminderKind Kind;
            public int IntervalMinutes;
            public DateTime NextDue;
            public double Accum;           // 高级模式：累计秒数
            public bool PendingClose;      // 简单模式：等待卡片关闭
            public DateTime PendingSince;
        }

        private readonly Timer _timer;
        private readonly AppConfig _config;
        private readonly ActivityMonitor _activity;
        private readonly Func<bool> _isPaused;
        private readonly Func<bool> _isReminderVisible;
        private readonly Func<bool> _isFullScreen;
        private Plan[] _plans = new Plan[0];
        private string _lastKey = "";
        private DateTime _lastTick = DateTime.Now;
        private double _activeMinuteAccum;   // 高级模式：用眼分钟累计（供统计）
        private bool _lastActiveLogged;
        private DateTime _lastSuppressLog = DateTime.MinValue;
        private readonly List<Plan> _dueScratch = new List<Plan>();   // 复用避免每秒分配
        private readonly List<Plan> _upcomingScratch = new List<Plan>();

        /// <summary>一组提醒到期（UI 线程触发；单元素 = 普通提醒，多元素 = 合并提醒）。</summary>
        public event Action<ReminderKind[]> ReminderDue;

        public Scheduler(AppConfig config, ActivityMonitor activity,
            Func<bool> isPaused, Func<bool> isReminderVisible, Func<bool> isFullScreen)
        {
            _config = config;
            _activity = activity;
            _isPaused = isPaused;
            _isReminderVisible = isReminderVisible;
            _isFullScreen = isFullScreen;

            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += OnTick;
        }

        public void Start()
        {
            RebuildPlans();
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        /// <summary>配置变更后调用；仅当模式/开关/间隔变化时重建计划。</summary>
        public void ApplyConfig()
        {
            if (BuildKey() == _lastKey)
            {
                return;
            }
            RebuildPlans();
        }

        /// <summary>睡眠唤醒：简单模式按当前时间重新对齐（含错峰）；高级模式累计值保留。</summary>
        public void OnPowerResume()
        {
            DateTime now = DateTime.Now;
            _lastTick = now;
            if (!_config.IsAdvanced)
            {
                RebuildPlans();
            }
        }

        /// <summary>
        /// 提醒卡片关闭时调用（自然完成或跳过均是）：
        /// 简单模式由此刻起排下一轮，任意两个提醒触发时刻至少相差 5 分钟（错峰）；
        /// 高级模式无需处理（关闭后累计自然继续）。
        /// </summary>
        public void OnReminderClosed(ReminderKind[] kinds)
        {
            if (_config.IsAdvanced || kinds == null || kinds.Length == 0)
            {
                return;
            }
            DateTime now = DateTime.Now;
            List<DateTime> occupied = new List<DateTime>();
            // 未参与本次提醒的计划占用的时间轴
            for (int i = 0; i < _plans.Length; i++)
            {
                if (_plans[i] != null && !ArrayContains(kinds, _plans[i].Kind))
                {
                    occupied.Add(_plans[i].NextDue);
                }
            }
            for (int k = 0; k < kinds.Length; k++)
            {
                Plan plan = FindPlan(kinds[k]);
                if (plan == null)
                {
                    continue;
                }
                plan.PendingClose = false;
                plan.NextDue = PlaceNextDue(now.AddMinutes(plan.IntervalMinutes), occupied);
                Log.Write(KindName(plan.Kind) + " next due=" + plan.NextDue.ToString("HH:mm:ss")
                    + " (spaced)");
            }
        }

        private static bool ArrayContains(ReminderKind[] array, ReminderKind value)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == value)
                {
                    return true;
                }
            }
            return false;
        }

        private void RebuildPlans()
        {
            DateTime now = DateTime.Now;
            Plan look = MakePlan(ReminderKind.Look, _config.LookEnabled);
            Plan blink = MakePlan(ReminderKind.Blink, _config.BlinkEnabled);
            Plan sit = MakePlan(ReminderKind.Sit, _config.SitEnabled);
            Plan water = MakePlan(ReminderKind.Water, _config.WaterEnabled);
            _plans = new Plan[] { look, blink, sit, water };

            // 简单模式：初始放置也做错峰，保证任意两个提醒相差 5 分钟以上
            List<DateTime> occupied = new List<DateTime>();
            PlaceAll(occupied, now, look, blink, sit, water);
            _lastKey = BuildKey();
            Log.Write("plans rebuilt: " + _lastKey);
        }

        private static Plan MakePlan(ReminderKind kind, bool enabled)
        {
            if (!enabled)
            {
                return null;
            }
            Plan p = new Plan();
            p.Kind = kind;
            return p;
        }

        private void PlaceAll(List<DateTime> occupied, DateTime now, params Plan[] plans)
        {
            for (int i = 0; i < plans.Length; i++)
            {
                if (plans[i] == null)
                {
                    continue;
                }
                plans[i].IntervalMinutes = IntervalOf(plans[i].Kind);
                plans[i].Accum = 0;
                plans[i].PendingClose = false;
                plans[i].NextDue = PlaceNextDue(now.AddMinutes(plans[i].IntervalMinutes), occupied);
            }
        }

        private int IntervalOf(ReminderKind kind)
        {
            switch (kind)
            {
                case ReminderKind.Look: return _config.LookIntervalMinutes;
                case ReminderKind.Blink: return _config.BlinkIntervalMinutes;
                case ReminderKind.Sit: return _config.SitIntervalMinutes;
                case ReminderKind.Water: return _config.WaterIntervalMinutes;
                default: return 60;
            }
        }

        /// <summary>在时间轴上放置一个触发时刻：与任何已占用时刻相差不足 5 分钟则向后推，直至满足。</summary>
        private static DateTime PlaceNextDue(DateTime desired, List<DateTime> occupied)
        {
            DateTime t = desired;
            bool moved = true;
            while (moved)
            {
                moved = false;
                for (int i = 0; i < occupied.Count; i++)
                {
                    DateTime o = occupied[i];
                    if (Math.Abs((t - o).TotalMinutes) < MinSpacingMinutes)
                    {
                        DateTime later = o >= t ? o : t;
                        t = later.AddMinutes(MinSpacingMinutes);
                        moved = true;
                        break;
                    }
                }
            }
            occupied.Add(t);
            return t;
        }

        private string BuildKey()
        {
            return (_config.IsAdvanced ? "A" : "S")
                + "|" + KeyPart(_config.LookEnabled, _config.LookIntervalMinutes)
                + "|" + KeyPart(_config.BlinkEnabled, _config.BlinkIntervalMinutes)
                + "|" + KeyPart(_config.SitEnabled, _config.SitIntervalMinutes)
                + "|" + KeyPart(_config.WaterEnabled, _config.WaterIntervalMinutes);
        }

        private static string KeyPart(bool enabled, int interval)
        {
            return enabled ? interval.ToString() : "off";
        }

        private Plan FindPlan(ReminderKind kind)
        {
            for (int i = 0; i < _plans.Length; i++)
            {
                if (_plans[i] != null && _plans[i].Kind == kind)
                {
                    return _plans[i];
                }
            }
            return null;
        }

        /// <summary>某类提醒距下一次触发的剩余秒数；未启用返回 -1；提醒进行中返回 0。</summary>
        public int GetRemainingSeconds(ReminderKind kind)
        {
            Plan plan = FindPlan(kind);
            if (plan == null)
            {
                return -1;
            }
            if (_config.IsAdvanced)
            {
                double remain = plan.IntervalMinutes * 60.0 - plan.Accum;
                return remain <= 0 ? 0 : (int)remain;
            }
            if (plan.PendingClose)
            {
                return 0;
            }
            double remainS = (plan.NextDue - DateTime.Now).TotalSeconds;
            return remainS <= 0 ? 0 : (int)remainS;
        }

        private static string KindName(ReminderKind kind)
        {
            switch (kind)
            {
                case ReminderKind.Look: return "look";
                case ReminderKind.Blink: return "blink";
                case ReminderKind.Sit: return "sit";
                case ReminderKind.Water: return "water";
                default: return kind.ToString();
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            double elapsed = (now - _lastTick).TotalSeconds;
            _lastTick = now;
            if (elapsed < 0 || elapsed > 10)
            {
                // 时钟跳变 / 系统卡顿保护：单次最多计 1 秒
                elapsed = 1;
            }

            if (_isPaused())
            {
                return;
            }

            bool fullscreen = _isFullScreen();

            if (_config.IsAdvanced)
            {
                bool active = false;
                try
                {
                    active = _activity.IsUserActive();
                }
                catch (Exception ex)
                {
                    Log.WriteError("activity-check", ex);
                }
                if (active != _lastActiveLogged)
                {
                    _lastActiveLogged = active;
                    Log.Write("activity change: active=" + active
                        + " idleSeconds=" + ActivityMonitor.GetIdleSeconds());
                }
                if (active && !_isReminderVisible())
                {
                    _activeMinuteAccum += elapsed;
                    if (_activeMinuteAccum >= 60.0)
                    {
                        _activeMinuteAccum -= 60.0;
                        StatsStore.RecordActiveMinute();
                    }
                }
                // 高级模式：同刻到期的提醒合并触发（累计制不做前瞻合并与错峰）
                _dueScratch.Clear();
                for (int i = 0; i < _plans.Length; i++)
                {
                    Plan plan = _plans[i];
                    if (plan == null)
                    {
                        continue;
                    }
                    if (active && !_isReminderVisible())
                    {
                        plan.Accum += elapsed;
                    }
                    if (plan.Accum >= plan.IntervalMinutes * 60.0)
                    {
                        _dueScratch.Add(plan);
                    }
                }
                if (_dueScratch.Count > 0)
                {
                    if (_isReminderVisible() || fullscreen)
                    {
                        LogSuppressed(_dueScratch[0]);
                    }
                    else
                    {
                        ReminderKind[] kinds = new ReminderKind[_dueScratch.Count];
                        for (int i = 0; i < _dueScratch.Count; i++)
                        {
                            kinds[i] = _dueScratch[i].Kind;
                            _dueScratch[i].Accum = 0;
                        }
                        Log.Write("due (merged): " + KindsLabel(kinds));
                        Fire(kinds);
                    }
                }
            }
            else
            {
                // 简单模式：收集到期与"5 分钟内即将到期"的计划
                _dueScratch.Clear();
                _upcomingScratch.Clear();
                for (int i = 0; i < _plans.Length; i++)
                {
                    Plan plan = _plans[i];
                    if (plan == null)
                    {
                        continue;
                    }
                    if (now >= plan.NextDue && !plan.PendingClose)
                    {
                        _dueScratch.Add(plan);
                    }
                    else if (!plan.PendingClose && (plan.NextDue - now).TotalMinutes <= MergeWindowMinutes)
                    {
                        _upcomingScratch.Add(plan);
                    }
                }

                if (_dueScratch.Count > 0)
                {
                    // 已过期超过合并窗口的计划不再等待，防止合并链式推迟过久
                    bool beyondWindow = false;
                    for (int i = 0; i < _dueScratch.Count; i++)
                    {
                        if ((now - _dueScratch[i].NextDue).TotalMinutes >= MergeWindowMinutes)
                        {
                            beyondWindow = true;
                            break;
                        }
                    }

                    if (_isReminderVisible() || fullscreen)
                    {
                        LogSuppressed(_dueScratch[0]);
                    }
                    else if (!beyondWindow && _upcomingScratch.Count > 0)
                    {
                        // 向下合并：即将到期的提醒在 5 分钟窗口内，等它们到期后一起触发
                        LogSuppressed(_dueScratch[0]);
                    }
                    else
                    {
                        ReminderKind[] kinds = new ReminderKind[_dueScratch.Count];
                        for (int i = 0; i < _dueScratch.Count; i++)
                        {
                            kinds[i] = _dueScratch[i].Kind;
                            _dueScratch[i].PendingClose = true;
                            _dueScratch[i].PendingSince = now;
                        }
                        Log.Write("due (merged): " + KindsLabel(kinds)
                            + ", waiting for card close to start next cycle");
                        Fire(kinds);
                    }
                }

                // 保险：卡片关闭回调异常未触发时，超时强制恢复计时
                for (int i = 0; i < _plans.Length; i++)
                {
                    Plan plan = _plans[i];
                    if (plan != null && plan.PendingClose
                        && (now - plan.PendingSince).TotalSeconds > PendingTimeoutSeconds)
                    {
                        Log.Write(KindName(plan.Kind) + " pending timeout, force reset");
                        plan.PendingClose = false;
                        plan.NextDue = now.AddMinutes(plan.IntervalMinutes);
                    }
                }
            }
        }

        private static string KindsLabel(ReminderKind[] kinds)
        {
            if (kinds == null || kinds.Length == 0)
            {
                return "none";
            }
            string s = KindName(kinds[0]);
            for (int i = 1; i < kinds.Length; i++)
            {
                s += "+" + KindName(kinds[i]);
            }
            return s;
        }

        private void LogSuppressed(Plan plan)
        {
            // 同一挂起状态最多每 30 秒记一条，避免刷屏
            if ((DateTime.Now - _lastSuppressLog).TotalSeconds < 30)
            {
                return;
            }
            _lastSuppressLog = DateTime.Now;
            Log.Write(KindName(plan.Kind) + " due but suppressed (visible card, fullscreen, or merging)");
        }

        private void Fire(ReminderKind[] kinds)
        {
            Action<ReminderKind[]> h = ReminderDue;
            if (h != null)
            {
                h(kinds);
            }
        }
    }
}
