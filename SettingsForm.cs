using System;
using System.Drawing;
using System.Windows.Forms;

namespace EyeCare20
{
    /// <summary>
    /// 设置窗口（从托盘右键打开）：计时模式 / 提醒项（望远·眨眼·久坐·喝水）/ 声音 / 自启动 / 检查更新。
    /// 改动即时保存并生效。
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        public event Action<AppConfig> ConfigChanged;

        private readonly AppConfig _config;
        private bool _loading = true;

        private readonly RadioButton _rbSimple;
        private readonly RadioButton _rbAdvanced;
        private readonly CheckBox _chkLook;
        private readonly NumericUpDown _numLookInterval;
        private readonly NumericUpDown _numLookDuration;
        private readonly CheckBox _chkBlink;
        private readonly NumericUpDown _numBlinkInterval;
        private readonly CheckBox _chkSit;
        private readonly NumericUpDown _numSitInterval;
        private readonly CheckBox _chkWater;
        private readonly NumericUpDown _numWaterInterval;
        private readonly CheckBox _chkSound;
        private readonly CheckBox _chkAutoStart;
        private readonly CheckBox _chkAutoUpdate;
        private readonly ComboBox _cmbLang;

        public SettingsForm(AppConfig config)
        {
            _config = config;

            Text = I18n.T("EyeCare20 设置");
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9.5F);
            ClientSize = new Size(420, 620);

            Color accent = Color.FromArgb(4, 138, 74);

            Label title = new Label();
            title.Text = I18n.T("设置");
            title.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(26, 26, 26);
            title.AutoSize = true;
            title.Location = new Point(24, 16);
            Controls.Add(title);

            // ---- 计时模式 ----
            GroupBox gMode = new GroupBox();
            gMode.Text = I18n.T(" 计时模式 ");
            gMode.ForeColor = accent;
            gMode.Bounds = new Rectangle(24, 54, 372, 106);
            Controls.Add(gMode);

            _rbSimple = new RadioButton();
            _rbSimple.Text = I18n.T("简单模式 · 按系统时间固定循环");
            _rbSimple.AutoSize = true;
            _rbSimple.Location = new Point(16, 28);
            _rbSimple.Checked = !_config.IsAdvanced;
            gMode.Controls.Add(_rbSimple);

            _rbAdvanced = new RadioButton();
            _rbAdvanced.Text = I18n.T("高级模式 · 仅操作电脑或播放音频时计时");
            _rbAdvanced.AutoSize = true;
            _rbAdvanced.Location = new Point(16, 60);
            _rbAdvanced.Checked = _config.IsAdvanced;
            gMode.Controls.Add(_rbAdvanced);

            // ---- 提醒项（四类统一） ----
            GroupBox gItems = new GroupBox();
            gItems.Text = I18n.T(" 提醒项 ");
            gItems.ForeColor = accent;
            gItems.Bounds = new Rectangle(24, 176, 372, 192);
            Controls.Add(gItems);

            _chkLook = AddItemRow(gItems, I18n.T("望远休息"), 0, _config.LookIntervalMinutes, 1, out _numLookInterval);
            _chkLook.Checked = _config.LookEnabled;

            _chkBlink = AddItemRow(gItems, I18n.T("眨眼训练"), 1, _config.BlinkIntervalMinutes, 1, out _numBlinkInterval);
            _chkBlink.Checked = _config.BlinkEnabled;

            _chkSit = AddItemRow(gItems, I18n.T("久坐提醒"), 2, _config.SitIntervalMinutes, 1, out _numSitInterval);
            _chkSit.Checked = _config.SitEnabled;

            _chkWater = AddItemRow(gItems, I18n.T("喝水提醒"), 3, _config.WaterIntervalMinutes, 1, out _numWaterInterval);
            _chkWater.Checked = _config.WaterEnabled;

            // 望远行附加：休息时长
            Label durLab = new Label();
            durLab.Text = I18n.T("时长");
            durLab.AutoSize = true;
            durLab.Location = new Point(252, 29);
            gItems.Controls.Add(durLab);

            _numLookDuration = new NumericUpDown();
            _numLookDuration.Minimum = 5;
            _numLookDuration.Maximum = 300;
            _numLookDuration.Width = 54;
            _numLookDuration.Value = Math.Min(Math.Max(_config.LookDurationSeconds, 5), 300);
            _numLookDuration.Location = new Point(286, 26);
            gItems.Controls.Add(_numLookDuration);

            Label secLab = new Label();
            secLab.Text = I18n.T("秒");
            secLab.AutoSize = true;
            secLab.Location = new Point(346, 29);
            gItems.Controls.Add(secLab);

            Label hint = new Label();
            hint.Text = I18n.T("到期弹出提醒卡片，卡片关闭后才开始该提醒的下一个周期");
            hint.Font = new Font("Microsoft YaHei UI", 8F);
            hint.ForeColor = Color.FromArgb(150, 150, 150);
            hint.AutoSize = true;
            hint.Location = new Point(14, 162);
            gItems.Controls.Add(hint);

            // ---- 其他 ----
            _chkSound = new CheckBox();
            _chkSound.Text = I18n.T("提醒时播放提示音");
            _chkSound.AutoSize = true;
            _chkSound.Location = new Point(28, 382);
            _chkSound.Checked = _config.SoundEnabled;
            Controls.Add(_chkSound);

            _chkAutoStart = new CheckBox();
            _chkAutoStart.Text = I18n.T("开机自动启动");
            _chkAutoStart.AutoSize = true;
            _chkAutoStart.Location = new Point(28, 414);
            _chkAutoStart.Checked = _config.AutoStart;
            Controls.Add(_chkAutoStart);

            // ---- 语言 ----
            GroupBox gLang = new GroupBox();
            gLang.Text = I18n.T(" 语言 ");
            gLang.ForeColor = accent;
            gLang.Bounds = new Rectangle(24, 446, 372, 56);
            Controls.Add(gLang);

            _cmbLang = new ComboBox();
            _cmbLang.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbLang.Location = new Point(16, 22);
            _cmbLang.Width = 180;
            _cmbLang.Items.AddRange(new object[] { I18n.T("自动（跟随系统）"), I18n.T("中文"), "English" });
            _cmbLang.SelectedIndex = SelectLangIndex(_config.Language);
            _cmbLang.SelectedIndexChanged += OnLangChanged;
            gLang.Controls.Add(_cmbLang);

            // ---- 检查更新 ----
            GroupBox gUpdate = new GroupBox();
            gUpdate.Text = I18n.T(" 检查更新 ");
            gUpdate.ForeColor = accent;
            gUpdate.Bounds = new Rectangle(24, 512, 372, 60);
            Controls.Add(gUpdate);

            _chkAutoUpdate = new CheckBox();
            _chkAutoUpdate.Text = I18n.T("启动时自动检查更新");
            _chkAutoUpdate.AutoSize = true;
            _chkAutoUpdate.Location = new Point(16, 26);
            _chkAutoUpdate.Checked = _config.AutoCheckUpdate;
            gUpdate.Controls.Add(_chkAutoUpdate);

            Label status = new Label();
            status.Text = I18n.T("改动即时生效并自动保存");
            status.Font = new Font("Microsoft YaHei UI", 8.75F);
            status.ForeColor = Color.FromArgb(140, 140, 140);
            status.AutoSize = true;
            status.Location = new Point(26, 588);
            Controls.Add(status);

            // ---- 事件（初始化完成后才挂接，避免误触发）----
            _rbSimple.CheckedChanged += OnChanged;
            _rbAdvanced.CheckedChanged += OnChanged;
            _chkLook.CheckedChanged += OnChanged;
            _numLookInterval.ValueChanged += OnChanged;
            _numLookDuration.ValueChanged += OnChanged;
            _chkBlink.CheckedChanged += OnChanged;
            _numBlinkInterval.ValueChanged += OnChanged;
            _chkSit.CheckedChanged += OnChanged;
            _numSitInterval.ValueChanged += OnChanged;
            _chkWater.CheckedChanged += OnChanged;
            _numWaterInterval.ValueChanged += OnChanged;
            _chkSound.CheckedChanged += OnChanged;
            _chkAutoStart.CheckedChanged += OnChanged;
            _chkAutoUpdate.CheckedChanged += OnChanged;

            _loading = false;
        }

        /// <summary>提醒项行：复选框 + 间隔数字（四类共用，紧凑不臃肿）。</summary>
        private static CheckBox AddItemRow(GroupBox parent, string name, int row,
            int intervalValue, int min, out NumericUpDown num)
        {
            int y = 26 + row * 34;

            CheckBox chk = new CheckBox();
            chk.Text = name;
            chk.AutoSize = true;
            chk.Location = new Point(14, y);
            parent.Controls.Add(chk);

            Label lab = new Label();
            lab.Text = I18n.T("间隔");
            lab.AutoSize = true;
            lab.Location = new Point(118, y + 3);
            parent.Controls.Add(lab);

            num = new NumericUpDown();
            num.Minimum = min;
            num.Maximum = 480;
            num.Width = 54;
            num.Value = Math.Min(Math.Max(intervalValue, min), 480);
            num.Location = new Point(152, y);
            parent.Controls.Add(num);

            Label unit = new Label();
            unit.Text = I18n.T("分钟");
            unit.AutoSize = true;
            unit.Location = new Point(212, y + 3);
            parent.Controls.Add(unit);

            return chk;
        }

        /// <summary>配置值 → ComboBox 索引："" → 0(自动), "zh" → 1, "en" → 2。</summary>
        private static int SelectLangIndex(string lang)
        {
            switch (lang)
            {
                case "zh": return 1;
                case "en": return 2;
                default: return 0;
            }
        }

        /// <summary>语言切换：保存配置后自动重启应用以应用新语言。避免初始化期间误触发。</summary>
        private void OnLangChanged(object sender, EventArgs e)
        {
            if (_loading)
            {
                return;
            }
            string newLang;
            switch (_cmbLang.SelectedIndex)
            {
                case 1: newLang = "zh"; break;
                case 2: newLang = "en"; break;
                default: newLang = ""; break;
            }
            // 值未变化则不重启
            if (newLang == _config.Language)
            {
                return;
            }
            _config.Language = newLang;
            ConfigStore.Save(_config);
            // 关闭设置窗口，重启应用
            Close();
            Program.Restart();
        }

        private void OnChanged(object sender, EventArgs e)
        {
            if (_loading)
            {
                return;
            }
            _config.TimerMode = _rbAdvanced.Checked ? "advanced" : "simple";
            _config.LookEnabled = _chkLook.Checked;
            _config.LookIntervalMinutes = (int)_numLookInterval.Value;
            _config.LookDurationSeconds = (int)_numLookDuration.Value;
            _config.BlinkEnabled = _chkBlink.Checked;
            _config.BlinkIntervalMinutes = (int)_numBlinkInterval.Value;
            _config.SitEnabled = _chkSit.Checked;
            _config.SitIntervalMinutes = (int)_numSitInterval.Value;
            _config.WaterEnabled = _chkWater.Checked;
            _config.WaterIntervalMinutes = (int)_numWaterInterval.Value;
            _config.SoundEnabled = _chkSound.Checked;
            _config.AutoStart = _chkAutoStart.Checked;
            _config.AutoCheckUpdate = _chkAutoUpdate.Checked;
            // UpdateUrl 不在界面暴露，保留配置文件中的值（此处禁止覆盖为空）

            ConfigStore.Save(_config);

            if (ConfigChanged != null)
            {
                ConfigChanged(_config);
            }
        }
    }
}
