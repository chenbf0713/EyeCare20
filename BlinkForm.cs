using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EyeCare20
{
    /// <summary>
    /// 眨眼训练提醒：屏幕正中卡片 + 眼睛开合动画，
    /// 引导"闭眼 2 秒 → 完整眨眼 ×5"，总时长约 12 秒。
    /// </summary>
    internal sealed class BlinkForm : CardForm
    {
        private const int ClosePhaseMs = 2000;   // 闭眼阶段
        private const int BlinkPhaseMs = 1200;   // 每次完整眨眼
        private const int DonePhaseMs = 1600;     // 结束提示
        private const int BlinkCount = 5;
        private const int TotalMs = ClosePhaseMs + BlinkPhaseMs * BlinkCount + DonePhaseMs;

        private static readonly Font TitleFont = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold);
        private static readonly Font StepFont = new Font("Microsoft YaHei UI", 12F);

        /// <summary>动画自然走完 = true（完成）；跳过/点击提前关闭 = false（跳过）。</summary>
        public bool Completed { get; private set; }

        private readonly System.Diagnostics.Stopwatch _watch;
        private Timer _animTimer;

        public BlinkForm()
            : base(440, 330)
        {
            _watch = System.Diagnostics.Stopwatch.StartNew();

            _animTimer = new Timer();
            _animTimer.Interval = 33;
            _animTimer.Tick += OnAnimTick;

            LinkLabel skip = CreateSkipLink();
            skip.Click += delegate
            {
                Completed = false;
                CloseWithFade();
            };
        }

        public override void Begin()
        {
            _animTimer.Start();
            StartInputWatch();
            Show();
            BeginFadeIn();
        }

        /// <summary>有效计时（扣除暂停时段，含未结算的当前暂停段）。</summary>
        private double EffectiveElapsedMs
        {
            get { return EffectiveElapsedFrom(_watch); }
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            if (EffectiveElapsedMs >= TotalMs)
            {
                _animTimer.Stop();
                Completed = true;
                CloseWithFade();
                return;
            }
            Invalidate();
        }

        private void GetPhase(double ms, out string text, out float openness)
        {
            if (ms < ClosePhaseMs)
            {
                // 闭眼阶段：眼睛慢慢闭合
                text = "闭上眼睛，保持 2 秒";
                openness = (float)(1.0 - ms / ClosePhaseMs);
                if (openness < 0F) openness = 0F;
            }
            else if (ms < ClosePhaseMs + BlinkPhaseMs * BlinkCount)
            {
                // 眨眼阶段：睁开 → 闭紧 → 保持闭眼，循环 5 次
                double t = (ms - ClosePhaseMs) % BlinkPhaseMs;
                int idx = (int)((ms - ClosePhaseMs) / BlinkPhaseMs) + 1;
                text = "完整眨眼 " + idx.ToString() + " / " + BlinkCount.ToString();
                if (t < 400)
                {
                    openness = (float)(t / 400.0);
                }
                else if (t < 800)
                {
                    openness = (float)(1.0 - (t - 400) / 400.0);
                }
                else
                {
                    openness = 0F;
                }
            }
            else if (ms < TotalMs)
            {
                text = "完成，眼睛轻松多了";
                double t = ms - ClosePhaseMs - BlinkPhaseMs * BlinkCount;
                openness = (float)Math.Min(1.0, t / 400.0);
            }
            else
            {
                text = "完成，眼睛轻松多了";
                openness = 1F;
            }
        }

        private void DrawEye(Graphics g, int cx, int cy, float openness)
        {
            int hw = 70;                              // 眼睛半宽
            int hh = Math.Max(2, (int)(62 * openness)); // 眼睛半高（随开合度变化）
            Point l = new Point(cx - hw, cy);
            Point r = new Point(cx + hw, cy);

            using (GraphicsPath eye = new GraphicsPath())
            {
                eye.AddBezier(l,
                    new Point(cx - hw * 2 / 3, cy - hh),
                    new Point(cx + hw * 2 / 3, cy - hh),
                    r);
                eye.AddBezier(r,
                    new Point(cx + hw * 2 / 3, cy + hh),
                    new Point(cx - hw * 2 / 3, cy + hh),
                    l);

                // 虹膜与瞳孔（裁剪在眼眶内）
                g.SetClip(eye);
                using (SolidBrush iris = new SolidBrush(Color.FromArgb(226, 244, 236)))
                {
                    g.FillEllipse(iris, cx - 27, cy - 27, 54, 54);
                }
                using (SolidBrush pupil = new SolidBrush(Color.FromArgb(40, 44, 42)))
                {
                    g.FillEllipse(pupil, cx - 13, cy - 13, 26, 26);
                }
                g.ResetClip();

                using (Pen outline = new Pen(Accent, 3.5F))
                {
                    g.DrawPath(outline, eye);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            DrawCenteredText(g, "眨眼训练", TitleFont, TextMain, 36);

            double ms = EffectiveElapsedMs;
            string text;
            float openness;
            GetPhase(ms, out text, out openness);

            DrawEye(g, Width / 2, 152, openness);
            if (CountdownPaused)
            {
                DrawCenteredText(g, PauseHintText, StepFont, WarnColor, 254);
            }
            else
            {
                DrawCenteredText(g, text, StepFont, TextSub, 254);
            }

            // 底部整体进度条（暂停时冻结并转为橙色）
            float progress = (float)(ms / TotalMs);
            if (progress < 0F) progress = 0F;
            if (progress > 1F) progress = 1F;
            using (SolidBrush bg = new SolidBrush(RingTrack))
            {
                g.FillRectangle(bg, 24, Height - 26, Width - 48, 4);
            }
            using (SolidBrush fg = new SolidBrush(CountdownPaused ? WarnColor : Accent))
            {
                g.FillRectangle(fg, 24, Height - 26, (Width - 48) * progress, 4);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Completed = false;
            CloseWithFade();   // 点击卡片任意位置提前结束
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_animTimer != null)
            {
                _animTimer.Stop();
            }
            base.OnFormClosed(e);
        }
    }
}
