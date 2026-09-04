using System;
using System.Collections.Generic;
using System.Globalization;

namespace EyeCare20
{
    /// <summary>
    /// 多语言支持：根据系统语言自动切换中英文，也可在设置中手动切换。
    /// 用法：I18n.T("望远休息") 返回当前语言的对应文本。
    /// </summary>
    internal static class I18n
    {
        private static string _lang = "zh";

        /// <summary>当前语言："zh" 或 "en"。</summary>
        public static string Lang { get { return _lang; } }

        public static bool IsEn { get { return _lang == "en"; } }

        /// <summary>中文→英文翻译表。中文模式下直接返回 key 本身。</summary>
        private static readonly Dictionary<string, string> EnDict = new Dictionary<string, string>
        {
            // ---- 通用 ----
            {"EyeCare20 护眼提醒", "EyeCare20 Eye Care"},
            {"EyeCare20 设置", "EyeCare20 Settings"},
            {"EyeCare20 统计", "EyeCare20 Stats"},

            // ---- 托盘菜单 ----
            {"主界面...", "Main window..."},
            {"设置...", "Settings..."},
            {"统计...", "Stats..."},
            {"检查更新...", "Check for updates..."},
            {"立即望远休息", "Rest now"},
            {"暂停 1 小时", "Pause 1 hour"},
            {"恢复提醒", "Resume reminders"},
            {"开机自启动", "Auto-start"},
            {"退出", "Exit"},

            // ---- 提醒名称与副标题 ----
            {"望远休息", "Look-away Rest"},
            {"眨眼训练", "Blink Training"},
            {"久坐提醒", "Sit Break"},
            {"喝水提醒", "Hydration"},
            {"闭眼 2 秒，完整眨眼 5 次", "Close eyes 2s, full blinks 5x"},
            {"站起来走动一下，伸展身体", "Stand up and move, stretch your body"},
            {"喝口水，给身体补充水分", "Drink water to stay hydrated"},
            {"看向 6 米外的物体，让眼睛放松一下", "Look at something 6m away to relax your eyes"},

            // ---- 主界面 ----
            {"卡片关闭后才开始下一个周期", "Next cycle starts after card closes"},
            {"高级模式 · 仅使用电脑时计时", "Advanced mode · counts only active use"},
            {"简单模式 · 按系统时间循环", "Simple mode · cycles by system time"},
            {"已暂停", "Paused"},
            {"已关闭", "Off"},
            {"提醒中…", "Active…"},

            // ---- 设置页 ----
            {"设置", "Settings"},
            {" 计时模式 ", " Timing Mode "},
            {"简单模式 · 按系统时间固定循环", "Simple mode · fixed cycle by system time"},
            {"高级模式 · 仅操作电脑或播放音频时计时", "Advanced mode · counts on PC activity or audio"},
            {" 提醒项 ", " Reminders "},
            {"时长", "Duration"},
            {"秒", "sec"},
            {"到期弹出提醒卡片，卡片关闭后才开始该提醒的下一个周期", "Reminder card pops when due; next cycle starts after card closes"},
            {"间隔", "Interval"},
            {"分钟", "min"},
            {"提醒时播放提示音", "Play sound on reminder"},
            {"开机自动启动", "Auto-start on boot"},
            {" 检查更新 ", " Updates "},
            {"启动时自动检查更新", "Auto-check for updates on startup"},
            {" Pro ", " Pro "},
            {"已激活 Pro ✓ 感谢支持！", "Pro activated ✓ Thank you!"},
            {"免费版", "Free version"},
            {"激活", "Activate"},
            {"改动即时生效并自动保存", "Changes apply instantly and auto-save"},
            {" 语言 ", " Language "},
            {"自动（跟随系统）", "Auto (follow system)"},
            {"中文", "Chinese"},
            {"English", "English"},
            {"激活成功，感谢支持！", "Activation successful, thank you!"},
            {"激活码无效，请检查后重试。", "Invalid activation code, please check and retry."},
            {"语言设置将在重启后生效。", "Language setting will take effect after restart."},

            // ---- 统计页 ----
            {"统计", "Stats"},
            {" 今日 ", " Today "},
            {"望远休息完成", "Look-away completed"},
            {"眨眼训练完成", "Blink training completed"},
            {"久坐提醒完成", "Sit break completed"},
            {"喝水提醒完成", "Hydration completed"},
            {"跳过提醒", "Skipped"},
            {"休息总时长", "Total rest time"},
            {"用眼时长", "Screen time"},
            {" 最近 7 天 · 完成次数 ", " Last 7 days · completions "},
            {"绿色 = 全部提醒完成次数 · 更新于打开时刻", "Green = total completions · updated on open"},
            {"暂无数据，完成第一次提醒后这里会出现柱状图", "No data yet. Complete a reminder to see the chart."},
            {"今天", "Today"},

            // ---- 更新 ----
            {"发现新版本", "New version available"},
            {"发现新版本 v", "New version available v"},
            {"当前版本 v", "Current version v"},
            {"（暂无更新说明）", "(No release notes)"},
            {"立即更新", "Update now"},
            {"以后再说", "Later"},
            {"正在更新", "Updating"},
            {"正在更新到 v", "Updating to v"},
            {"正在下载…", "Downloading…"},
            {"正在解包…", "Extracting…"},
            {"正在安装…", "Installing…"},
            {"更新完成，正在重启…", "Update complete, restarting…"},
            {"下载完成后软件将自动重启", "App will auto-restart after download"},
            {"取消", "Cancel"},
            {"更新失败：", "Update failed: "},
            {"启动替换脚本失败，请手动更新", "Failed to launch replace script, please update manually"},
            {"检查更新失败，请稍后重试。\n（请确认网络可用，或更新源地址可访问）", "Update check failed, please try again later.\n(Ensure network is available and update source is accessible)"},

            // ---- 提醒卡 ----
            {"跳过", "Skip"},
        };

        /// <summary>初始化语言：配置值为空时自动检测系统语言。</summary>
        public static void Init(string configLang)
        {
            if (configLang == "zh" || configLang == "en")
            {
                _lang = configLang;
            }
            else
            {
                AutoDetect();
            }
        }

        /// <summary>自动检测系统语言：英文环境→en，其他→zh。</summary>
        public static void AutoDetect()
        {
            try
            {
                string twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                _lang = (twoLetter == "en") ? "en" : "zh";
            }
            catch
            {
                _lang = "zh";
            }
        }

        /// <summary>手动设置语言。</summary>
        public static void SetLang(string lang)
        {
            _lang = (lang == "en") ? "en" : "zh";
        }

        /// <summary>翻译文本：中文模式下返回原文，英文模式下查表返回译文。</summary>
        public static string T(string text)
        {
            if (_lang == "en" && text != null && EnDict.ContainsKey(text))
            {
                return EnDict[text];
            }
            return text;
        }

        // ---- 复合格式化辅助 ----

        /// <summary>"X 次" → en: "X times"</summary>
        public static string Times(int count)
        {
            return _lang == "en"
                ? count + " times"
                : count + " 次";
        }

        /// <summary>"当前已是最新版本（vX）" 格式。</summary>
        public static string AlreadyLatest(string version)
        {
            return _lang == "en"
                ? "Already up to date (v" + version + ")."
                : "当前已是最新版本（v" + version + "）。";
        }

        /// <summary>剩余时间后缀："MM:SS 后提醒" → en: "in MM:SS"</summary>
        public static string RemainingSuffix(string timeText)
        {
            return _lang == "en"
                ? "in " + timeText
                : timeText + " 后提醒";
        }

        /// <summary>格式化时长（秒→"X 小时 Y 分" / "Xh Ym"）。</summary>
        public static string FormatDuration(int totalSeconds)
        {
            if (totalSeconds <= 0)
            {
                return _lang == "en" ? "0s" : "0 秒";
            }
            int h = totalSeconds / 3600;
            int m = (totalSeconds % 3600) / 60;
            int s = totalSeconds % 60;
            if (_lang == "en")
            {
                if (h > 0) return h + "h " + m + "m";
                if (m > 0) return m + "m " + s + "s";
                return s + "s";
            }
            else
            {
                if (h > 0) return h + " 小时 " + m + " 分";
                if (m > 0) return m + " 分 " + s + " 秒";
                return s + " 秒";
            }
        }
    }
}
