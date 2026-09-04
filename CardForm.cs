using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace EyeCare20
{
    /// <summary>
    /// 提醒卡片基类：屏幕正中、无边框圆角、置顶但不抢焦点（WS_EX_NOACTIVATE）、淡入淡出。
    /// </summary>
    internal abstract class CardForm : Form
    {
        protected static readonly Color Accent = Color.FromArgb(4, 138, 74);     // #048A4A
        protected static readonly Color TextMain = Color.FromArgb(26, 26, 26);
        protected static readonly Color TextSub = Color.FromArgb(112, 112, 112);
        protected static readonly Color RingTrack = Color.FromArgb(233, 233, 233);
        protected static readonly Color WarnColor = Color.FromArgb(214, 122, 39);

        private readonly Timer _fadeTimer;
        private bool _fadingOut;

        // ---- 键鼠/全屏活动检测（休息中冻结倒计时） ----
        private const double InputActiveSeconds = 2.0;   // 距上次输入 ≤ 2 秒视为“在操作”
        private Timer _inputTimer;
        private double _pausedMs;      // 累计暂停毫秒
        private bool _pauseOpen;
        private DateTime _pauseStart;
        private bool _pauseByInput;      // 当前暂停原因：键鼠操作
        private bool _pauseByFullscreen; // 当前暂停原因：前台全屏应用

        /// <summary>倒计时因键鼠操作或全屏应用暂停中。</summary>
        protected bool CountdownPaused
        {
            get { return _pauseOpen; }
        }

        /// <summary>累计暂停毫秒（子类从计时经过中扣除）。</summary>
        protected double PausedMs
        {
            get { return _pausedMs; }
        }

        /// <summary>
        /// 有效计时毫秒 = 总经过 − 累计暂停 − 当前暂停段（未结算部分实时计入，
        /// 否则暂停期间倒计时照走、走完即关闭，暂停形同虚设）。
        /// </summary>
        protected double EffectiveElapsedFrom(System.Diagnostics.Stopwatch watch)
        {
            double paused = _pausedMs;
            if (_pauseOpen)
            {
                paused += (DateTime.Now - _pauseStart).TotalMilliseconds;
            }
            return watch.Elapsed.TotalMilliseconds - paused;
        }

        /// <summary>暂停原因的界面提示文案。</summary>
        protected string PauseHintText
        {
            get
            {
                if (_pauseByInput && _pauseByFullscreen)
                {
                    return "检测到键鼠操作或全屏应用 · 倒计时已暂停";
                }
                if (_pauseByInput)
                {
                    return "检测到键鼠操作 · 倒计时已暂停";
                }
                return "检测到全屏应用 · 倒计时已暂停";
            }
        }

        protected CardForm(int width, int height)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint, true);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.White;
            Opacity = 0.0;

            Size = new Size(width, height);
            Rectangle sb = Screen.PrimaryScreen.Bounds;
            Location = new Point(sb.Left + (sb.Width - width) / 2, sb.Top + (sb.Height - height) / 2);
            Region = CreateRoundedRegion(width, height, 18);

            _fadeTimer = new Timer();
            _fadeTimer.Interval = 16;
            _fadeTimer.Tick += OnFadeTick;
        }

        /// <summary>子类实现：启动动画并显示。</summary>
        public abstract void Begin();

        /// <summary>
        /// 启动键鼠/全屏活动监测：键鼠操作中（距上次输入 ≤ 2 秒）或前台为全屏应用
        /// （游戏/电影等沉浸使用）→ 暂停倒计时；停止操作且退出全屏 → 恢复。
        /// 锁屏例外：锁屏 = 人已离开 = 休息中，倒计时继续。
        /// </summary>
        protected void StartInputWatch()
        {
            if (_inputTimer != null)
            {
                return;
            }
            _inputTimer = new Timer();
            _inputTimer.Interval = 500;
            _inputTimer.Tick += OnActivityTick;
            _inputTimer.Start();
        }

        private void OnActivityTick(object sender, EventArgs e)
        {
            bool inputActive;
            try
            {
                inputActive = ActivityMonitor.GetIdleSeconds() <= InputActiveSeconds;
            }
            catch (Exception)
            {
                inputActive = false;
            }
            bool fullscreen = IsFullScreenForPause();

            if (inputActive || fullscreen)
            {
                if (!_pauseOpen)
                {
                    _pauseOpen = true;
                    _pauseStart = DateTime.Now;
                    _pauseByInput = inputActive;
                    _pauseByFullscreen = fullscreen;
                    Log.Write("countdown paused (" + PauseReasonLogText + ")");
                    OnCountdownPauseChanged(true);
                }
                else
                {
                    // 暂停中原因可能切换（如键鼠停了但前台全屏），刷新显示
                    _pauseByInput = inputActive;
                    _pauseByFullscreen = fullscreen;
                }
            }
            else if (_pauseOpen)
            {
                _pauseOpen = false;
                _pausedMs += (DateTime.Now - _pauseStart).TotalMilliseconds;
                _pauseByInput = false;
                _pauseByFullscreen = false;
                Log.Write("countdown resumed");
                OnCountdownPauseChanged(false);
            }
        }

        private string PauseReasonLogText
        {
            get
            {
                if (_pauseByInput && _pauseByFullscreen)
                {
                    return "input+fullscreen";
                }
                return _pauseByInput ? "input" : "fullscreen";
            }
        }

        /// <summary>全屏暂停判定（排除锁屏：锁屏期间视为休息中，不暂停）。</summary>
        private static bool IsFullScreenForPause()
        {
            try
            {
                if (IsLockScreenForeground())
                {
                    return false;
                }
                return FullScreenDetector.IsFullScreen();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsLockScreenForeground()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero)
            {
                return false;
            }
            StringBuilder sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            return sb.ToString() == "Windows.UI.Core.CoreWindow";
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        /// <summary>暂停状态变化通知（子类可重写以刷新界面）。</summary>
        protected virtual void OnCountdownPauseChanged(bool paused)
        {
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000;  // WS_EX_NOACTIVATE：不抢键盘焦点
                cp.ExStyle |= 0x00000080;  // WS_EX_TOOLWINDOW：不出现在 Alt-Tab
                return cp;
            }
        }

        public void BeginFadeIn()
        {
            _fadingOut = false;
            _fadeTimer.Start();
        }

        protected void CloseWithFade()
        {
            if (_fadingOut)
            {
                return;
            }
            _fadingOut = true;
            _fadeTimer.Start();
        }

        private void OnFadeTick(object sender, EventArgs e)
        {
            if (_fadingOut)
            {
                if (Opacity <= 0.06)
                {
                    _fadeTimer.Stop();
                    Close();
                }
                else
                {
                    Opacity -= 0.08;
                }
            }
            else
            {
                if (Opacity >= 1.0)
                {
                    _fadeTimer.Stop();
                }
                else
                {
                    Opacity = Math.Min(1.0, Opacity + 0.10);
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _fadeTimer.Stop();
            if (_inputTimer != null)
            {
                _inputTimer.Stop();
            }
            base.OnFormClosed(e);
        }

        protected static Region CreateRoundedRegion(int w, int h, int r)
        {
            int d = r * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(w - d, 0, d, d, 270, 90);
            path.AddArc(w - d, h - d, d, d, 0, 90);
            path.AddArc(0, h - d, d, d, 90, 90);
            path.CloseFigure();
            return new Region(path);
        }

        protected void DrawCenteredText(Graphics g, string text, Font font, Color color, int y)
        {
            using (SolidBrush b = new SolidBrush(color))
            {
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(text, font, b,
                        new RectangleF(0, y, ClientRectangle.Width, font.Height * 2 + 8), sf);
                }
            }
        }

        protected LinkLabel CreateSkipLink()
        {
            LinkLabel skip = new LinkLabel();
            skip.Text = I18n.T("跳过");
            skip.Font = new Font("Microsoft YaHei UI", 9.75F);
            skip.LinkColor = Color.FromArgb(150, 150, 150);
            skip.ActiveLinkColor = Accent;
            skip.VisitedLinkColor = Color.FromArgb(150, 150, 150);
            skip.LinkBehavior = LinkBehavior.HoverUnderline;
            skip.AutoSize = true;
            skip.Location = new Point(Width - 68, Height - 46);
            Controls.Add(skip);
            return skip;
        }
    }
}
