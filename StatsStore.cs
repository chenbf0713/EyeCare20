using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace EyeCare20
{
    /// <summary>单日统计数据（按提醒类型分别计数）。</summary>
    [DataContract]
    public class DayStats
    {
        [DataMember(Name = "LookDone")]
        public int LookDone;

        [DataMember(Name = "BlinkDone")]
        public int BlinkDone;

        [DataMember(Name = "SitDone")]
        public int SitDone;

        [DataMember(Name = "WaterDone")]
        public int WaterDone;

        [DataMember(Name = "Skipped")]
        public int Skipped;

        [DataMember(Name = "RestSeconds")]
        public int RestSeconds;

        [DataMember(Name = "ActiveMinutes")]
        public int ActiveMinutes;

        public int DoneCount(ReminderKind kind)
        {
            switch (kind)
            {
                case ReminderKind.Look: return LookDone;
                case ReminderKind.Blink: return BlinkDone;
                case ReminderKind.Sit: return SitDone;
                case ReminderKind.Water: return WaterDone;
                default: return 0;
            }
        }

        public int TotalDone
        {
            get { return LookDone + BlinkDone + SitDone + WaterDone; }
        }
    }

    /// <summary>
    /// 按日统计持久化：%APPDATA%\EyeCare20\stats.json，键为 "yyyy-MM-dd"。
    /// 内存持有 + 脏标志 + 定时 Flush，避免频繁磁盘 IO。
    /// </summary>
    public static class StatsStore
    {
        private static readonly object Gate = new object();
        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EyeCare20");
        private static readonly string FilePath = Path.Combine(Dir, "stats.json");

        private static Dictionary<string, DayStats> _days;
        private static bool _dirty;
        private static bool _loaded;

        /// <summary>记录一次提醒完成（倒计时自然走完）。</summary>
        public static void RecordDone(ReminderKind kind)
        {
            lock (Gate)
            {
                switch (kind)
                {
                    case ReminderKind.Look: Today().LookDone++; break;
                    case ReminderKind.Blink: Today().BlinkDone++; break;
                    case ReminderKind.Sit: Today().SitDone++; break;
                    case ReminderKind.Water: Today().WaterDone++; break;
                }
                _dirty = true;
            }
        }

        public static void RecordSkip()
        {
            lock (Gate)
            {
                Today().Skipped++;
                _dirty = true;
            }
        }

        public static void RecordRest(int seconds)
        {
            if (seconds <= 0)
            {
                return;
            }
            lock (Gate)
            {
                Today().RestSeconds += seconds;
                _dirty = true;
            }
        }

        /// <summary>高级模式：记录 1 分钟实际用眼时长。</summary>
        public static void RecordActiveMinute()
        {
            lock (Gate)
            {
                Today().ActiveMinutes++;
                _dirty = true;
            }
        }

        /// <summary>获取指定日期统计（不存在返回空记录，不落盘）。</summary>
        public static DayStats GetDay(DateTime date)
        {
            lock (Gate)
            {
                EnsureLoaded();
                DayStats stats;
                if (_days.TryGetValue(date.ToString("yyyy-MM-dd"), out stats))
                {
                    return stats;
                }
                return new DayStats();
            }
        }

        /// <summary>脏数据落盘（定时和退出时调用）。</summary>
        public static void Flush()
        {
            lock (Gate)
            {
                if (!_dirty)
                {
                    return;
                }
                try
                {
                    EnsureLoaded();
                    if (!Directory.Exists(Dir))
                    {
                        Directory.CreateDirectory(Dir);
                    }
                    using (MemoryStream ms = new MemoryStream())
                    {
                        DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(Dictionary<string, DayStats>));
                        ser.WriteObject(ms, _days);
                        File.WriteAllBytes(FilePath, ms.ToArray());
                    }
                    _dirty = false;
                }
                catch (Exception ex)
                {
                    Log.WriteError("stats-flush", ex);
                }
            }
        }

        private static DayStats Today()
        {
            EnsureLoaded();
            string key = DateTime.Now.ToString("yyyy-MM-dd");
            DayStats stats;
            if (!_days.TryGetValue(key, out stats))
            {
                stats = new DayStats();
                _days[key] = stats;
            }
            return stats;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;
            _days = new Dictionary<string, DayStats>();
            try
            {
                if (File.Exists(FilePath))
                {
                    byte[] bytes = File.ReadAllBytes(FilePath);
                    // 兼容 UTF-8 BOM
                    int offset = (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0;
                    using (MemoryStream ms = new MemoryStream(bytes, offset, bytes.Length - offset, false))
                    {
                        DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(Dictionary<string, DayStats>));
                        object obj = ser.ReadObject(ms);
                        Dictionary<string, DayStats> days = obj as Dictionary<string, DayStats>;
                        if (days != null)
                        {
                            _days = days;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("stats-load", ex);
                _days = new Dictionary<string, DayStats>();
            }
        }
    }
}
