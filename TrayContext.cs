using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EyeCare20
{
    /// <summary>
    /// 托盘常驻上下文：右键托盘 = 设置及常规菜单；双击托盘 = 立即望远休息。
    /// </summary>
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly AppConfig _config;
        private readonly NotifyIcon _trayIcon;
        private readonly Scheduler _scheduler;
        private readonly ActivityMonitor _activity;
        private readonly ToolStripMenuItem _miPause;
        private readonly ToolStripMenuItem _miAutoStart;
        private readonly SynchronizationContext _sync;

        private CardForm _card;
        private ReminderKind[] _cardKinds = new ReminderKind[0];
        private MainForm _mainForm;
        private SettingsForm _settingsForm;
        private StatsForm _statsForm;
        private UpdateNoticeForm _updateForm;
        private UpdateProgressForm _updateProgressForm;
        private DateTime _pausedUntil = DateTime.MinValue;
        private bool _exiting;
        private readonly System.Windows.Forms.Timer _flushTimer;
        private readonly System.Windows.Forms.Timer _updateCheckTimer;

        public TrayContext()
        {
            Log.Write("TrayContext ctor begin");
            _sync = SynchronizationContext.Current;

            _config = ConfigStore.Load();
            I18n.Init(_config.Language);
            Log.Write("config loaded mode=" + _config.TimerMode
                + " lookInterval=" + _config.LookIntervalMinutes
                + " lookDuration=" + _config.LookDurationSeconds
                + " lang=" + I18n.Lang);
            if (_config.AutoStart)
            {
                AutoStart.SetEnabled(true);
            }

            _activity = new ActivityMonitor();

            _miPause = new ToolStripMenuItem(I18n.T("暂停 1 小时"));
            _miPause.Click += OnPauseClick;

            _miAutoStart = new ToolStripMenuItem(I18n.T("开机自启动"));
            _miAutoStart.Checked = _config.AutoStart;
            _miAutoStart.Click += OnAutoStartClick;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Microsoft YaHei UI", 9.5F);
            menu.Items.Add(new ToolStripMenuItem(I18n.T("主界面..."), null, OnMainFormClick));
            menu.Items.Add(new ToolStripMenuItem(I18n.T("设置..."), null, OnSettingsClick));
            menu.Items.Add(new ToolStripMenuItem(I18n.T("统计..."), null, OnStatsClick));
            menu.Items.Add(new ToolStripMenuItem(I18n.T("检查更新..."), null, OnUpdateCheckClick));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(I18n.T("立即望远休息"), null, OnRestNowClick));
            menu.Items.Add(_miPause);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_miAutoStart);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(I18n.T("退出"), null, OnExitClick));

            _trayIcon = new NotifyIcon();
            _trayIcon.Icon = TrayIconPainter.CreateIcon();
            _trayIcon.Text = I18n.T("EyeCare20 护眼提醒");
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.Visible = true;
            _trayIcon.DoubleClick += OnMainFormClick;

            _scheduler = new Scheduler(
                _config,
                _activity,
                delegate { return _pausedUntil > DateTime.Now; },
                delegate { return IsReminderVisible(); },
                delegate { return FullScreenDetector.IsFullScreen(); });
            _scheduler.ReminderDue += OnReminderDue;
            Log.Write("scheduler starting");
            _scheduler.Start();

            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            // 统计定时落盘（30 秒）
            _flushTimer = new System.Windows.Forms.Timer();
            _flushTimer.Interval = 30000;
            _flushTimer.Tick += delegate { StatsStore.Flush(); };
            _flushTimer.Start();

            // 启动 8 秒后自动检查更新（可在设置中关闭；URL 为空则跳过）
            _updateCheckTimer = new System.Windows.Forms.Timer();
            _updateCheckTimer.Interval = 8000;
            _updateCheckTimer.Tick += delegate
            {
                _updateCheckTimer.Stop();
                if (_config.AutoCheckUpdate)
                {
                    CheckUpdate(false);
                }
            };
            _updateCheckTimer.Start();

            Log.Write("TrayContext ctor end");
        }

        private bool IsReminderVisible()
        {
            return _card != null && !_card.IsDisposed;
        }

        private void OnReminderDue(ReminderKind[] kinds)
        {
            ShowReminder(kinds);
        }

        private void ShowReminder(ReminderKind[] kinds)
        {
            if (_exiting || IsReminderVisible() || kinds == null || kinds.Length == 0)
            {
                return;
            }
            if (_config.SoundEnabled)
            {
                System.Media.SystemSounds.Exclamation.Play();
            }
            CardForm card = CreateCard(kinds);
            _card = card;
            _cardKinds = kinds;
            card.FormClosed += delegate
            {
                Log.Write("card closed (" + KindLabel(kinds[0]) + (kinds.Length > 1 ? " +" + (kinds.Length - 1) + " merged" : "") + ")");
                RecordCardStats(kinds, card);
                _scheduler.OnReminderClosed(kinds);
                _card = null;
            };
            Log.Write("card shown: " + KindLabel(kinds[0]) + (kinds.Length > 1 ? " merged x" + kinds.Length : ""));
            card.Begin();
        }

        private CardForm CreateCard(ReminderKind[] kinds)
        {
            if (kinds.Length == 1 && kinds[0] == ReminderKind.Blink)
            {
                return new BlinkForm();
            }
            ReminderItem[] items = new ReminderItem[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                items[i] = MakeItem(kinds[i]);
            }
            return new ReminderForm(items);
        }

        private ReminderItem MakeItem(ReminderKind kind)
        {
            ReminderItem item = new ReminderItem();
            item.Kind = kind;
            switch (kind)
            {
                case ReminderKind.Blink:
                    item.Title = I18n.T("眨眼训练");
                    item.Subtitle = I18n.T("闭眼 2 秒，完整眨眼 5 次");
                    item.Icon = ReminderIconKind.Eye;
                    item.DurationSeconds = 12;
                    break;
                case ReminderKind.Sit:
                    item.Title = I18n.T("久坐提醒");
                    item.Subtitle = I18n.T("站起来走动一下，伸展身体");
                    item.Icon = ReminderIconKind.Person;
                    item.DurationSeconds = 30;
                    break;
                case ReminderKind.Water:
                    item.Title = I18n.T("喝水提醒");
                    item.Subtitle = I18n.T("喝口水，给身体补充水分");
                    item.Icon = ReminderIconKind.Drop;
                    item.DurationSeconds = 15;
                    break;
                case ReminderKind.Look:
                default:
                    item.Title = I18n.T("望远休息");
                    item.Subtitle = I18n.T("看向 6 米外的物体，让眼睛放松一下");
                    item.Icon = ReminderIconKind.Eye;
                    item.DurationSeconds = _config.LookDurationSeconds;
                    break;
            }
            return item;
        }

        private void RecordCardStats(ReminderKind[] kinds, CardForm card)
        {
            try
            {
                bool completed;
                int restedSeconds = 0;
                ReminderForm rf = card as ReminderForm;
                if (rf != null)
                {
                    completed = rf.Completed;
                    restedSeconds = rf.RestedSeconds;
                }
                else
                {
                    BlinkForm bf = card as BlinkForm;
                    completed = bf != null && bf.Completed;
                }
                for (int i = 0; i < kinds.Length; i++)
                {
                    if (completed)
                    {
                        StatsStore.RecordDone(kinds[i]);
                    }
                    else
                    {
                        StatsStore.RecordSkip();
                    }
                }
                if (restedSeconds > 0)
                {
                    StatsStore.RecordRest(restedSeconds);
                }
                StatsStore.Flush();
                RefreshStatsForm();
            }
            catch (Exception ex)
            {
                Log.WriteError("stats-card", ex);
            }
        }

        private static string KindLabel(ReminderKind kind)
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

        private void RefreshStatsForm()
        {
            if (_statsForm != null && !_statsForm.IsDisposed)
            {
                _statsForm.RefreshData();
            }
        }

        private void OnRestNowClick(object sender, EventArgs e)
        {
            ShowReminder(new ReminderKind[] { ReminderKind.Look });
        }

        /// <summary>双击托盘 / 菜单“主界面”：打开主界面（各提醒剩余时间）。</summary>
        private void OnMainFormClick(object sender, EventArgs e)
        {
            if (_mainForm != null && !_mainForm.IsDisposed)
            {
                _mainForm.Activate();
                return;
            }
            _mainForm = new MainForm(_scheduler, _config,
                delegate { return _pausedUntil > DateTime.Now; },
                GetActiveCardKinds);
            _mainForm.RestNowRequested += delegate { ShowReminder(new ReminderKind[] { ReminderKind.Look }); };
            _mainForm.Show();
            Log.Write("main form opened");
        }

        private ReminderKind[] GetActiveCardKinds()
        {
            return IsReminderVisible() ? _cardKinds : new ReminderKind[0];
        }

        private void OnPauseClick(object sender, EventArgs e)
        {
            if (_pausedUntil > DateTime.Now)
            {
                _pausedUntil = DateTime.MinValue;
                _miPause.Text = I18n.T("暂停 1 小时");
            }
            else
            {
                _pausedUntil = DateTime.Now.AddHours(1);
                _miPause.Text = I18n.T("恢复提醒");
            }
        }

        private void OnAutoStartClick(object sender, EventArgs e)
        {
            _miAutoStart.Checked = !_miAutoStart.Checked;
            _config.AutoStart = _miAutoStart.Checked;
            AutoStart.SetEnabled(_config.AutoStart);
            ConfigStore.Save(_config);
        }

        private void OnSettingsClick(object sender, EventArgs e)
        {
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                _settingsForm.Activate();
                return;
            }
            _settingsForm = new SettingsForm(_config);
            _settingsForm.ConfigChanged += OnConfigChanged;
            _settingsForm.Show();
        }

        private void OnStatsClick(object sender, EventArgs e)
        {
            if (_statsForm != null && !_statsForm.IsDisposed)
            {
                _statsForm.Activate();
                _statsForm.RefreshData();
                return;
            }
            _statsForm = new StatsForm(_config.IsAdvanced);
            _statsForm.Show();
        }

        private void OnUpdateCheckClick(object sender, EventArgs e)
        {
            CheckUpdate(true);
        }

        /// <summary>自动更新：弹出进度窗；下载完成、替换脚本就绪后退出本进程（重启由脚本完成）。</summary>
        private void StartAutoUpdate(UpdateInfo info)
        {
            if (_updateProgressForm != null && !_updateProgressForm.IsDisposed)
            {
                _updateProgressForm.Activate();
                return;
            }
            Log.Write("auto update started: " + info.Version);
            _updateProgressForm = new UpdateProgressForm(info, delegate
            {
                Log.Write("update: exiting for replacement");
                _exiting = true;
                ExitThread();
            });
            _updateProgressForm.Show();
        }

        /// <summary>检查更新：后台线程按序尝试多个 update.json 源，结果封送回 UI 线程。
        /// UpdateUrl 为空 = 内置源（Gitee 优先 → GitHub 回退）；非空可用 "|" 分隔多个自定义源。</summary>
        private void CheckUpdate(bool manual)
        {
            System.Collections.Generic.List<string> urls = new System.Collections.Generic.List<string>();
            string custom = (_config.UpdateUrl ?? "").Trim();
            if (custom.Length > 0)
            {
                string[] parts = custom.Split('|');
                for (int i = 0; i < parts.Length; i++)
                {
                    string u = parts[i].Trim();
                    if (u.Length > 0)
                    {
                        urls.Add(u);
                    }
                }
            }
            else
            {
                urls.AddRange(UpdateSources.BuiltinUpdateJsonUrls);
            }
            if (urls.Count == 0)
            {
                return;
            }
            Log.Write("update check start (manual=" + manual + ", sources=" + urls.Count + ")");
            string[] urlArray = urls.ToArray();
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                UpdateInfo info = UpdateChecker.CheckAny(urlArray);
                if (_sync == null)
                {
                    return;
                }
                _sync.Post(delegate(object s2)
                {
                    if (_exiting)
                    {
                        return;
                    }
                    if (info == null)
                    {
                        Log.Write("update check failed (all sources)");
                        if (manual)
                        {
                            MessageBox.Show(I18n.T("检查更新失败，请稍后重试。\n（请确认网络可用，或更新源地址可访问）"),
                                "EyeCare20", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        return;
                    }
                    if (!UpdateChecker.IsNewer(info))
                    {
                        Log.Write("update check: already latest, remote=" + info.Version);
                        if (manual)
                        {
                            MessageBox.Show(I18n.AlreadyLatest(UpdateChecker.CurrentVersion().ToString()),
                                "EyeCare20", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        return;
                    }
                    Log.Write("update available: " + info.Version);
                    if (_updateForm == null || _updateForm.IsDisposed)
                    {
                        _updateForm = new UpdateNoticeForm(info);
                        _updateForm.InstallRequested += StartAutoUpdate;
                        _updateForm.Show();
                    }
                    else
                    {
                        _updateForm.Activate();
                    }
                }, null);
            }, null);
        }

        private void OnConfigChanged(AppConfig cfg)
        {
            _scheduler.ApplyConfig();

            // 同步注册表自启动项（以配置为准）
            bool enabled = AutoStart.IsEnabled();
            if (_config.AutoStart && !enabled)
            {
                AutoStart.SetEnabled(true);
            }
            else if (!_config.AutoStart && enabled)
            {
                AutoStart.SetEnabled(false);
            }
            _miAutoStart.Checked = _config.AutoStart;
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume && _sync != null)
            {
                // SystemEvents 可能从非 UI 线程回调，先封送到 UI 线程
                _sync.Post(delegate(object state) { _scheduler.OnPowerResume(); }, null);
            }
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            _exiting = true;
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            if (_card != null && !_card.IsDisposed) _card.Close();
            if (_mainForm != null && !_mainForm.IsDisposed) _mainForm.Close();
            if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.Close();
            if (_statsForm != null && !_statsForm.IsDisposed) _statsForm.Close();
            if (_updateForm != null && !_updateForm.IsDisposed) _updateForm.Close();
            if (_updateProgressForm != null && !_updateProgressForm.IsDisposed) _updateProgressForm.Close();

            SystemEvents.PowerModeChanged -= OnPowerModeChanged;

            if (_flushTimer != null) _flushTimer.Stop();
            if (_updateCheckTimer != null) _updateCheckTimer.Stop();
            if (_scheduler != null) _scheduler.Stop();
            if (_activity != null) _activity.Dispose();
            StatsStore.Flush();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            base.ExitThreadCore();
        }
    }

    /// <summary>程序内自绘托盘图标：#048A4A 眼睛 + 白色瞳孔，无 .ico 资源依赖。</summary>
    internal static class TrayIconPainter
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon CreateIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    using (GraphicsPath eye = new GraphicsPath())
                    {
                        Point l = new Point(3, 16);
                        Point r = new Point(29, 16);
                        eye.AddBezier(l, new Point(10, 5), new Point(22, 5), r);
                        eye.AddBezier(r, new Point(22, 27), new Point(10, 27), l);
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(4, 138, 74)))
                        {
                            g.FillPath(b, eye);
                        }
                    }
                    using (SolidBrush w = new SolidBrush(Color.White))
                    {
                        g.FillEllipse(w, 12, 11, 8, 10);
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(hIcon).Clone();
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }
    }
}
