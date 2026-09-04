using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EyeCare20
{
    /// <summary>提醒卡片标题左侧的矢量小图标类型。</summary>
    internal enum ReminderIconKind
    {
        Eye,     // 望远休息
        Person,  // 久坐提醒
        Drop     // 喝水提醒
    }

    /// <summary>单个提醒事项的展示信息。</summary>
    internal sealed class ReminderItem
    {
        public ReminderKind Kind;
        public string Title;
        public string Subtitle;
        public ReminderIconKind Icon;
        public int DurationSeconds;
    }

    /// <summary>
    /// 通用倒计时提醒卡片：屏幕正中 + 环形倒计时 + 标题左侧矢量小图标。
    /// 支持单事项（大标题 + 副标题）与多事项合并（图标横排 + 名称串联 + 事项列表）。
    /// 点击卡片任意位置或"跳过"可提前结束。
    /// </summary>
    internal sealed class ReminderForm : CardForm
    {
        /// <summary>倒计时自然走完 = true（完成）；点击提前关闭 = false（跳过）。</summary>
        public bool Completed { get; private set; }

        /// <summary>实际停留秒数（关闭时刻的经过时间）。</summary>
        public int RestedSeconds { get; private set; }

        private readonly ReminderItem[] _items;
        private readonly bool _single;
        private readonly int _durationSeconds;
        private readonly System.Diagnostics.Stopwatch _watch;
        private Timer _animTimer;

        private static readonly Font TitleFont = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold);
        private static readonly Font MergedTitleFont = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold);
        private static readonly Font MergedNamesFont = new Font("Microsoft YaHei UI", 10.5F);
        private static readonly Font MergedLineFont = new Font("Microsoft YaHei UI", 8.5F);
        private static readonly Font NumFont = new Font("Microsoft YaHei UI", 30F, FontStyle.Bold);
        private static readonly Font SubFont = new Font("Microsoft YaHei UI", 11F);

        public ReminderForm(ReminderItem[] items)
            : base(440, 330)
        {
            if (items == null || items.Length == 0)
            {
                throw new ArgumentException("items 不能为空", "items");
            }
            _items = items;
            _single = items.Length == 1;
            int maxDur = 5;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].DurationSeconds > maxDur)
                {
                    maxDur = items[i].DurationSeconds;
                }
            }
            _durationSeconds = maxDur;
            _watch = System.Diagnostics.Stopwatch.StartNew();

            _animTimer = new Timer();
            _animTimer.Interval = 50;
            _animTimer.Tick += OnAnimTick;

            LinkLabel skip = CreateSkipLink();
            skip.Click += delegate
            {
                MarkSkipped();
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

        private void MarkSkipped()
        {
            Completed = false;
            RestedSeconds = (int)(EffectiveElapsedMs / 1000.0 + 0.5);
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            double remainingMs = _durationSeconds * 1000.0 - EffectiveElapsedMs;
            if (remainingMs <= 0)
            {
                _animTimer.Stop();
                Completed = true;
                RestedSeconds = _durationSeconds;
                CloseWithFade();
                return;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            try
            {
                PaintCard(e.Graphics);
            }
            catch (Exception ex)
            {
                Log.WriteError("card-paint", ex);
            }
        }

        private void PaintCard(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (_single)
            {
                PaintHeaderSingle(g);
            }
            else
            {
                PaintHeaderMerged(g);
            }

            // 环形倒计时（两类布局共用位置；有效计时扣除暂停时段）
            double remainingMs = _durationSeconds * 1000.0 - EffectiveElapsedMs;
            if (remainingMs < 0)
            {
                remainingMs = 0;
            }
            float frac = (float)(remainingMs / (_durationSeconds * 1000.0));
            int cx = Width / 2;
            int cy = 176;
            int r = 62;

            using (Pen track = new Pen(RingTrack, 10F))
            {
                g.DrawEllipse(track, cx - r, cy - r, r * 2, r * 2);
            }
            if (frac > 0.005F)
            {
                using (Pen arc = new Pen(CountdownPaused ? WarnColor : Accent, 10F))
                {
                    arc.StartCap = LineCap.Round;
                    arc.EndCap = LineCap.Round;
                    g.DrawArc(arc, cx - r, cy - r, r * 2, r * 2, -90F, 360F * frac);
                }
            }

            // 剩余秒数（暂停时橙色提示）
            int secs = (int)Math.Ceiling(remainingMs / 1000.0);
            using (SolidBrush nb = new SolidBrush(CountdownPaused ? WarnColor : Accent))
            {
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(secs.ToString(), NumFont, nb,
                        new RectangleF(cx - r, cy - r, r * 2, r * 2), sf);
                }
            }

            if (CountdownPaused)
            {
                DrawCenteredText(g, PauseHintText, SubFont, WarnColor, 268);
            }
            else if (_single)
            {
                DrawCenteredText(g, _items[0].Subtitle, SubFont, TextSub, 268);
            }
            else
            {
                PaintLinesMerged(g);
            }
        }

        /// <summary>单事项：大标题 + 左侧小图标。</summary>
        private void PaintHeaderSingle(Graphics g)
        {
            ReminderItem item = _items[0];
            SizeF titleSize = g.MeasureString(item.Title, TitleFont);
            int iconSize = 30;
            int iconX = (int)(Width / 2f - titleSize.Width / 2f - iconSize - 14f);
            VectorIcons.Draw(g, item.Icon, iconX, 35, iconSize, Accent);
            DrawCenteredText(g, item.Title, TitleFont, TextMain, 36);
        }

        /// <summary>多事项：小图标横排 + "同时提醒" + 名称串联。</summary>
        private void PaintHeaderMerged(Graphics g)
        {
            int iconSize = 22;
            int gap = 10;
            int totalW = _items.Length * iconSize + (_items.Length - 1) * gap;
            int x = (Width - totalW) / 2;
            for (int i = 0; i < _items.Length; i++)
            {
                VectorIcons.Draw(g, _items[i].Icon, x, 30, iconSize, Accent);
                x += iconSize + gap;
            }
            DrawCenteredText(g, I18n.T("同时提醒"), MergedTitleFont, TextMain, 60);

            string names = _items[0].Title;
            for (int i = 1; i < _items.Length; i++)
            {
                names += " · " + _items[i].Title;
            }
            using (SolidBrush b = new SolidBrush(Accent))
            {
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(names, MergedNamesFont, b,
                        new RectangleF(0, 94, Width, 24), sf);
                }
            }
        }

        /// <summary>多事项：底部逐行显示各事项提示（最多 3 行）。</summary>
        private void PaintLinesMerged(Graphics g)
        {
            const int maxLines = 3;
            int count = Math.Min(_items.Length, maxLines);
            int y = 254;
            using (SolidBrush b = new SolidBrush(TextSub))
            {
                for (int i = 0; i < count; i++)
                {
                    if (i == maxLines - 1 && _items.Length > maxLines)
                    {
                        g.DrawString("…… 等共 " + _items.Length + " 件事", MergedLineFont, b, 40, y);
                        break;
                    }
                    g.DrawString("· " + _items[i].Title + "：" + _items[i].Subtitle, MergedLineFont, b, 40, y);
                    y += 20;
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            MarkSkipped();
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

    /// <summary>极简矢量小图标（GDI+ 自绘，零资源文件）。</summary>
    internal static class VectorIcons
    {
        public static void Draw(Graphics g, ReminderIconKind kind, int x, int y, int size, Color color)
        {
            switch (kind)
            {
                case ReminderIconKind.Person:
                    DrawPerson(g, x, y, size, color);
                    break;
                case ReminderIconKind.Drop:
                    DrawDrop(g, x, y, size, color);
                    break;
                default:
                    DrawEye(g, x, y, size, color);
                    break;
            }
        }

        /// <summary>眼睛（望远休息）。</summary>
        private static void DrawEye(Graphics g, float x, float y, float s, Color color)
        {
            PointF l = new PointF(x + 2, y + s / 2f);
            PointF r = new PointF(x + s - 2, y + s / 2f);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddBezier(l, new PointF(x + s * 0.3f, y + s * 0.12f),
                    new PointF(x + s * 0.7f, y + s * 0.12f), r);
                path.AddBezier(r, new PointF(x + s * 0.7f, y + s * 0.88f),
                    new PointF(x + s * 0.3f, y + s * 0.88f), l);
                using (Pen pen = new Pen(color, 2.6f))
                {
                    g.DrawPath(pen, path);
                }
                g.SetClip(path);
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.FillEllipse(b, x + s * 0.36f, y + s * 0.3f, s * 0.28f, s * 0.4f);
                }
                g.ResetClip();
            }
        }

        /// <summary>伸展小人（久坐提醒）。</summary>
        private static void DrawPerson(Graphics g, float x, float y, float s, Color color)
        {
            float cx = x + s / 2f;
            using (Pen pen = new Pen(color, Math.Max(2.5f, s / 11f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                // 头
                g.DrawEllipse(pen, cx - s * 0.12f, y, s * 0.24f, s * 0.24f);
                // 躯干
                g.DrawLine(pen, cx, y + s * 0.28f, cx, y + s * 0.58f);
                // 上举手臂（伸展姿态）
                g.DrawLine(pen, cx, y + s * 0.35f, x + s * 0.14f, y + s * 0.1f);
                g.DrawLine(pen, cx, y + s * 0.35f, x + s * 0.86f, y + s * 0.1f);
                // 双腿
                g.DrawLine(pen, cx, y + s * 0.58f, x + s * 0.22f, y + s * 0.95f);
                g.DrawLine(pen, cx, y + s * 0.58f, x + s * 0.78f, y + s * 0.95f);
            }
        }

        /// <summary>水滴（喝水提醒）。</summary>
        private static void DrawDrop(Graphics g, float x, float y, float s, Color color)
        {
            float peakX = x + s / 2f, peakY = y + s * 0.02f;
            float rightX = x + s * 0.85f, leftX = x + s * 0.15f, midY = y + s * 0.55f;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddLine(peakX, peakY, rightX, midY);
                path.AddArc(leftX, midY - s * 0.28f, s * 0.7f, s * 0.56f, 0, 180);
                path.AddLine(leftX, midY, peakX, peakY);
                path.CloseFigure();
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.FillPath(b, path);
                }
            }
        }
    }
}
