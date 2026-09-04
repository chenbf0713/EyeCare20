using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EyeCare20
{
    /// <summary>
    /// 主界面（双击托盘图标打开）：显示各提醒的下次剩余时间，支持手动立即望远休息。
    /// 剩余时间每秒刷新。
    /// </summary>
    internal sealed class MainForm : Form
    {
        public event EventHandler RestNowRequested;

        private static readonly Color Accent = Color.FromArgb(4, 138, 74);
        private static readonly Color TextMain = Color.FromArgb(26, 26, 26);
        private static readonly Color TextSub = Color.FromArgb(112, 112, 112);
        private static readonly Color TextOff = Color.FromArgb(170, 170, 170);
        private static readonly Color WarnColor = Color.FromArgb(214, 122, 39);

        private readonly Scheduler _scheduler;
        private readonly AppConfig _config;
        private readonly Func<bool> _isPaused;
        private readonly Func<ReminderKind[]> _getActiveKinds;
        private readonly MainPanel _panel;
        private readonly System.Windows.Forms.Timer _timer;

        public MainForm(Scheduler scheduler, AppConfig config,
            Func<bool> isPaused, Func<ReminderKind[]> getActiveKinds)
        {
            _scheduler = scheduler;
            _config = config;
            _isPaused = isPaused;
            _getActiveKinds = getActiveKinds;

            Text = "EyeCare20";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9.5F);
            ClientSize = new Size(400, 386);
            Icon = TrayIconPainter.CreateIcon();

            _panel = new MainPanel(this);
            _panel.Dock = DockStyle.Fill;
            Controls.Add(_panel);

            Button restBtn = new Button();
            restBtn.Text = I18n.T("立即望远休息");
            restBtn.FlatStyle = FlatStyle.Flat;
            restBtn.FlatAppearance.BorderColor = Accent;
            restBtn.ForeColor = Color.White;
            restBtn.BackColor = Accent;
            restBtn.Cursor = Cursors.Hand;
            restBtn.Size = new Size(150, 36);
            restBtn.Location = new Point(24, ClientSize.Height - 56);
            restBtn.Click += delegate
            {
                EventHandler h = RestNowRequested;
                if (h != null)
                {
                    h(this, EventArgs.Empty);
                }
            };
            Controls.Add(restBtn);
            restBtn.BringToFront();

            Label hint = new Label();
            hint.Text = I18n.T("卡片关闭后才开始下一个周期");
            hint.Font = new Font("Microsoft YaHei UI", 8.25F);
            hint.ForeColor = Color.FromArgb(150, 150, 150);
            hint.AutoSize = true;
            hint.Location = new Point(190, ClientSize.Height - 46);
            Controls.Add(hint);

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 1000;
            _timer.Tick += delegate { _panel.Invalidate(); };
            _timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            base.OnFormClosed(e);
        }

        private static string FormatRemaining(int seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }
            if (seconds >= 3600)
            {
                return (seconds / 3600) + ":" + (seconds % 3600 / 60).ToString("00")
                    + ":" + (seconds % 60).ToString("00");
            }
            return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }

        /// <summary>主界面主体：模式说明 + 四类提醒剩余时间（自绘，每秒刷新）。</summary>
        private sealed class MainPanel : Panel
        {
            private static readonly ReminderKind[] Kinds =
            {
                ReminderKind.Look, ReminderKind.Blink, ReminderKind.Sit, ReminderKind.Water
            };
            private static string[] GetNames()
            {
                return new string[] { I18n.T("望远休息"), I18n.T("眨眼训练"), I18n.T("久坐提醒"), I18n.T("喝水提醒") };
            }
            private static readonly ReminderIconKind[] Icons =
            {
                ReminderIconKind.Eye, ReminderIconKind.Eye,
                ReminderIconKind.Person, ReminderIconKind.Drop
            };

            private readonly MainForm _owner;

            public MainPanel(MainForm owner)
            {
                _owner = owner;
                SetStyle(ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.UserPaint, true);
                BackColor = Color.White;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // 顶部：标题 + 模式/暂停状态
                using (Font titleFont = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(TextMain))
                {
                    g.DrawString("EyeCare20", titleFont, tb, 24, 18);
                }
                bool paused = _owner._isPaused();
                string modeText = paused
                    ? I18n.T("已暂停")
                    : (_owner._config.IsAdvanced ? I18n.T("高级模式 · 仅使用电脑时计时") : I18n.T("简单模式 · 按系统时间循环"));
                using (Font modeFont = new Font("Microsoft YaHei UI", 8.75F))
                using (SolidBrush mb = new SolidBrush(paused ? WarnColor : TextSub))
                {
                    g.DrawString(modeText, modeFont, mb, 26, 48);
                }

                ReminderKind[] active = _owner._getActiveKinds() ?? new ReminderKind[0];
                string[] names = GetNames();

                for (int i = 0; i < Kinds.Length; i++)
                {
                    int y = 76 + i * 62;
                    int remaining = _owner._scheduler.GetRemainingSeconds(Kinds[i]);

                    VectorIcons.Draw(g, Icons[i], 26, y + 6, 30, remaining < 0 ? TextOff : Accent);

                    using (Font nameFont = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold))
                    using (SolidBrush nb = new SolidBrush(remaining < 0 ? TextOff : TextMain))
                    {
                        g.DrawString(names[i], nameFont, nb, 68, y + 4);
                    }

                    bool isActive = Array.IndexOf(active, Kinds[i]) >= 0;
                    string status;
                    Color statusColor;
                    if (remaining < 0)
                    {
                        status = I18n.T("已关闭");
                        statusColor = TextOff;
                    }
                    else if (isActive)
                    {
                        status = I18n.T("提醒中…");
                        statusColor = Accent;
                    }
                    else if (paused)
                    {
                        status = I18n.T("已暂停");
                        statusColor = TextOff;
                    }
                    else
                    {
                        status = I18n.RemainingSuffix(FormatRemaining(remaining));
                        statusColor = TextSub;
                    }
                    using (Font stFont = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold))
                    using (SolidBrush sb = new SolidBrush(statusColor))
                    {
                        SizeF sz = g.MeasureString(status, stFont);
                        g.DrawString(status, stFont, sb, Width - 26 - sz.Width, y + 7);
                    }

                    using (Pen line = new Pen(Color.FromArgb(240, 240, 240)))
                    {
                        g.DrawLine(line, 24, y + 48, Width - 24, y + 48);
                    }
                }
            }
        }
    }
}
