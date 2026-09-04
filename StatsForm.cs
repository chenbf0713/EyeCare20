using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EyeCare20
{
    /// <summary>
    /// 统计窗口（从托盘右键打开）：今日摘要 + 最近 7 天柱状图。
    /// </summary>
    internal sealed class StatsForm : Form
    {
        private static readonly Color Accent = Color.FromArgb(4, 138, 74);

        private readonly bool _advancedMode;
        private readonly ChartPanel _chart;
        private readonly Label[] _kindVals;
        private readonly Label _lblSkip;
        private readonly Label _lblRest;
        private readonly Label _lblActive;

        public StatsForm(bool advancedMode)
        {
            _advancedMode = advancedMode;

            Text = I18n.T("EyeCare20 统计");
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9.5F);

            int rows = 4 + 2 + (_advancedMode ? 1 : 0);
            int todayH = rows * 30 + 24;
            int chartY = 54 + todayH + 16;
            ClientSize = new Size(440, chartY + 214 + 42);

            Label title = new Label();
            title.Text = I18n.T("统计");
            title.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(26, 26, 26);
            title.AutoSize = true;
            title.Location = new Point(24, 16);
            Controls.Add(title);

            // ---- 今日 ----
            GroupBox gToday = new GroupBox();
            gToday.Text = I18n.T(" 今日 ");
            gToday.ForeColor = Accent;
            gToday.Bounds = new Rectangle(24, 54, 392, todayH);
            Controls.Add(gToday);

            string[] kindLabels = { I18n.T("望远休息完成"), I18n.T("眨眼训练完成"), I18n.T("久坐提醒完成"), I18n.T("喝水提醒完成") };
            _kindVals = new Label[4];
            for (int i = 0; i < 4; i++)
            {
                _kindVals[i] = AddRow(gToday, kindLabels[i], i);
            }
            _lblSkip = AddRow(gToday, I18n.T("跳过提醒"), 4);
            _lblRest = AddRow(gToday, I18n.T("休息总时长"), 5);
            _lblActive = _advancedMode ? AddRow(gToday, I18n.T("用眼时长"), 6) : null;

            // ---- 最近 7 天 ----
            GroupBox gChart = new GroupBox();
            gChart.Text = I18n.T(" 最近 7 天 · 完成次数 ");
            gChart.ForeColor = Accent;
            gChart.Bounds = new Rectangle(24, chartY, 392, 214);
            Controls.Add(gChart);

            _chart = new ChartPanel();
            _chart.Bounds = new Rectangle(10, 22, 372, 182);
            _chart.BackColor = Color.White;
            gChart.Controls.Add(_chart);

            Label tip = new Label();
            tip.Text = I18n.T("绿色 = 全部提醒完成次数 · 更新于打开时刻");
            tip.Font = new Font("Microsoft YaHei UI", 8.75F);
            tip.ForeColor = Color.FromArgb(140, 140, 140);
            tip.AutoSize = true;
            tip.Location = new Point(26, chartY + 226);
            Controls.Add(tip);

            RefreshData();
        }

        private Label AddRow(GroupBox parent, string labelText, int row)
        {
            int y = 22 + row * 30;
            Label lab = new Label();
            lab.Text = labelText;
            lab.AutoSize = true;
            lab.Location = new Point(16, y);
            parent.Controls.Add(lab);

            Label val = new Label();
            val.Text = I18n.Times(0);
            val.AutoSize = true;
            val.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            val.ForeColor = Color.FromArgb(26, 26, 26);
            val.Location = new Point(300, y - 1);
            parent.Controls.Add(val);
            return val;
        }

        public void RefreshData()
        {
            DayStats today = StatsStore.GetDay(DateTime.Now);
            for (int i = 0; i < 4; i++)
            {
                _kindVals[i].Text = I18n.Times(today.DoneCount((ReminderKind)i));
            }
            _lblSkip.Text = I18n.Times(today.Skipped);
            _lblRest.Text = I18n.FormatDuration(today.RestSeconds);
            if (_advancedMode && _lblActive != null)
            {
                _lblActive.Text = I18n.FormatDuration(today.ActiveMinutes * 60);
            }
            _chart.Invalidate();
        }

        /// <summary>最近 7 天完成次数柱状图（自绘）。</summary>
        private sealed class ChartPanel : Panel
        {
            public ChartPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.UserPaint, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                DateTime today = DateTime.Now;
                int[] values = new int[7];
                int max = 0;
                for (int i = 0; i < 7; i++)
                {
                    DayStats d = StatsStore.GetDay(today.AddDays(i - 6));
                    values[i] = d.TotalDone;
                    if (values[i] > max)
                    {
                        max = values[i];
                    }
                }

                int left = 8, right = Width - 8;
                int top = 26, bottom = Height - 26;
                int axisMax = Math.Max(4, (max + 1) / 2 * 2);   // 刻度向上取偶

                using (Pen axis = new Pen(Color.FromArgb(225, 225, 225)))
                {
                    // 横向网格线 + 刻度值
                    using (Font f = new Font("Microsoft YaHei UI", 8F))
                    using (SolidBrush gray = new SolidBrush(Color.FromArgb(160, 160, 160)))
                    {
                        for (int grid = 0; grid <= 2; grid++)
                        {
                            int v = axisMax * grid / 2;
                            int y = bottom - (bottom - top) * grid / 2;
                            g.DrawLine(axis, left, y, right, y);
                            g.DrawString(v.ToString(), f, gray, 2, y - 7);
                        }
                    }

                    int slot = (right - left) / 7;
                    int barW = Math.Min(30, slot - 12);
                    using (SolidBrush green = new SolidBrush(Color.FromArgb(4, 138, 74)))
                    using (Font numFont = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold))
                    using (SolidBrush numBrush = new SolidBrush(Color.FromArgb(70, 70, 70)))
                    using (Font dateFont = new Font("Microsoft YaHei UI", 8F))
                    using (SolidBrush dateBrush = new SolidBrush(Color.FromArgb(150, 150, 150)))
                    {
                        bool any = max > 0;
                        for (int i = 0; i < 7; i++)
                        {
                            int cx = left + slot * i + slot / 2;
                            DateTime day = today.AddDays(i - 6);
                            if (any && values[i] > 0)
                            {
                                int barH = (int)((bottom - top) * (double)values[i] / axisMax);
                                if (barH < 2)
                                {
                                    barH = 2;
                                }
                                g.FillRectangle(green, cx - barW / 2, bottom - barH, barW, barH);
                                g.DrawString(values[i].ToString(), numFont, numBrush,
                                    cx - 8, bottom - barH - 18);
                            }
                            string label = (i == 6) ? I18n.T("今天") : day.ToString("MM-dd");
                            g.DrawString(label, dateFont, dateBrush, cx - 16, bottom + 6);
                        }
                        if (!any)
                        {
                            using (Font f = new Font("Microsoft YaHei UI", 9.5F))
                            using (SolidBrush gray = new SolidBrush(Color.FromArgb(160, 160, 160)))
                            {
                                using (StringFormat sf = new StringFormat())
                                {
                                    sf.Alignment = StringAlignment.Center;
                                    g.DrawString(I18n.T("暂无数据，完成第一次提醒后这里会出现柱状图"), f, gray,
                                        new RectangleF(0, (top + bottom) / 2 - 12, Width, 24), sf);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
