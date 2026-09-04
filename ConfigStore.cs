using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace EyeCare20
{
    /// <summary>提醒类型。</summary>
    public enum ReminderKind
    {
        Look = 0,   // 望远休息
        Blink = 1,  // 眨眼训练
        Sit = 2,    // 久坐提醒
        Water = 3   // 喝水提醒
    }

    /// <summary>应用配置。默认值：简单模式 / 望远20分钟 / 时长20秒 / 眨眼30分钟 / 久坐60分钟 / 喝水45分钟。</summary>
    [DataContract]
    public class AppConfig
    {
        [DataMember(Name = "TimerMode")]
        public string TimerMode;

        [DataMember(Name = "LookIntervalMinutes")]
        public int LookIntervalMinutes;

        [DataMember(Name = "LookDurationSeconds")]
        public int LookDurationSeconds;

        [DataMember(Name = "BlinkIntervalMinutes")]
        public int BlinkIntervalMinutes;

        [DataMember(Name = "AutoStart")]
        public bool AutoStart;

        [DataMember(Name = "SoundEnabled")]
        public bool SoundEnabled;

        [DataMember(Name = "LookEnabled")]
        public bool LookEnabled;

        [DataMember(Name = "BlinkEnabled")]
        public bool BlinkEnabled;

        [DataMember(Name = "SitEnabled")]
        public bool SitEnabled;

        [DataMember(Name = "SitIntervalMinutes")]
        public int SitIntervalMinutes;

        [DataMember(Name = "WaterEnabled")]
        public bool WaterEnabled;

        [DataMember(Name = "WaterIntervalMinutes")]
        public int WaterIntervalMinutes;

        [DataMember(Name = "UpdateUrl")]
        public string UpdateUrl;

        [DataMember(Name = "AutoCheckUpdate")]
        public bool AutoCheckUpdate;

        [DataMember(Name = "Language")]
        public string Language;

        public bool IsAdvanced
        {
            get { return string.Equals(TimerMode, "advanced", StringComparison.OrdinalIgnoreCase); }
        }

        public AppConfig()
        {
            SetDefaults();
        }

        private void SetDefaults()
        {
            TimerMode = "simple";
            LookIntervalMinutes = 20;
            LookDurationSeconds = 20;
            BlinkIntervalMinutes = 30;
            AutoStart = false;
            SoundEnabled = true;
            LookEnabled = true;
            BlinkEnabled = true;
            SitEnabled = true;
            SitIntervalMinutes = 60;
            WaterEnabled = true;
            WaterIntervalMinutes = 45;
            UpdateUrl = "";
            AutoCheckUpdate = true;
            Language = "";
        }

        /// <summary>DataContract 反序列化不调用构造函数，用此回调补默认值。</summary>
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            SetDefaults();
        }
    }

    /// <summary>配置持久化：%APPDATA%\EyeCare20\config.json</summary>
    public static class ConfigStore
    {
        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EyeCare20");

        private static readonly string FilePath = Path.Combine(Dir, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    byte[] bytes = File.ReadAllBytes(FilePath);
                    // 兼容记事本等编辑器写入的 UTF-8 BOM（DataContractJsonSerializer 不接受 BOM）
                    int offset = (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0;
                    using (MemoryStream ms = new MemoryStream(bytes, offset, bytes.Length - offset, false))
                    {
                        DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(AppConfig));
                        object obj = ser.ReadObject(ms);
                        AppConfig cfg = obj as AppConfig;
                        if (cfg != null)
                        {
                            Clamp(cfg);
                            return cfg;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return new AppConfig();
        }

        public static void Save(AppConfig cfg)
        {
            try
            {
                if (!Directory.Exists(Dir))
                {
                    Directory.CreateDirectory(Dir);
                }
                using (FileStream fs = new FileStream(FilePath, FileMode.Create, FileAccess.Write))
                {
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(AppConfig));
                    ser.WriteObject(fs, cfg);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void Clamp(AppConfig cfg)
        {
            if (cfg.LookIntervalMinutes < 1) cfg.LookIntervalMinutes = 20;
            if (cfg.LookIntervalMinutes > 480) cfg.LookIntervalMinutes = 480;
            if (cfg.LookDurationSeconds < 5) cfg.LookDurationSeconds = 20;
            if (cfg.LookDurationSeconds > 300) cfg.LookDurationSeconds = 300;
            if (cfg.BlinkIntervalMinutes < 1) cfg.BlinkIntervalMinutes = 30;
            if (cfg.BlinkIntervalMinutes > 480) cfg.BlinkIntervalMinutes = 480;
            if (cfg.SitIntervalMinutes < 1) cfg.SitIntervalMinutes = 60;
            if (cfg.SitIntervalMinutes > 480) cfg.SitIntervalMinutes = 480;
            if (cfg.WaterIntervalMinutes < 1) cfg.WaterIntervalMinutes = 45;
            if (cfg.WaterIntervalMinutes > 480) cfg.WaterIntervalMinutes = 480;
            if (!string.Equals(cfg.TimerMode, "advanced", StringComparison.OrdinalIgnoreCase))
            {
                cfg.TimerMode = "simple";
            }
            if (cfg.UpdateUrl == null)
            {
                cfg.UpdateUrl = "";
            }
        }
    }
}
