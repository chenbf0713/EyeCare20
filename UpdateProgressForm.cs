using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace EyeCare20
{
    /// <summary>
    /// 自动更新进度窗：后台下载 → 解包 → 启动替换脚本 → 请求宿主退出（重启由脚本完成）。
    /// </summary>
    internal sealed class UpdateProgressForm : Form
    {
        private readonly UpdateInfo _info;
        private readonly Action _onInstallReady;   // 安装就绪：宿主应立即退出（UI 线程回调）
        private readonly SynchronizationContext _sync;
        private readonly ProgressBar _bar;
        private readonly Label _lblStatus;
        private readonly Label _lblDetail;
        private volatile bool _cancelled;

        private static readonly Color Accent = Color.FromArgb(4, 138, 74);

        public UpdateProgressForm(UpdateInfo info, Action onInstallReady)
        {
            _info = info;
            _onInstallReady = onInstallReady;
            _sync = SynchronizationContext.Current;

            Text = "正在更新";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9.5F);
            ClientSize = new Size(380, 168);

            Label title = new Label();
            title.Text = "正在更新到 v" + info.Version;
            title.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            title.ForeColor = Accent;
            title.AutoSize = true;
            title.Location = new Point(24, 18);
            Controls.Add(title);

            _lblStatus = new Label();
            _lblStatus.Text = "正在下载…";
            _lblStatus.ForeColor = Color.FromArgb(60, 60, 60);
            _lblStatus.AutoSize = true;
            _lblStatus.Location = new Point(26, 58);
            Controls.Add(_lblStatus);

            _bar = new ProgressBar();
            _bar.SetBounds(24, 84, 332, 12);
            Controls.Add(_bar);

            _lblDetail = new Label();
            _lblDetail.Text = "";
            _lblDetail.Font = new Font("Microsoft YaHei UI", 8.5F);
            _lblDetail.ForeColor = Color.FromArgb(150, 150, 150);
            _lblDetail.AutoSize = true;
            _lblDetail.Location = new Point(26, 102);
            Controls.Add(_lblDetail);

            Label hint = new Label();
            hint.Text = "下载完成后软件将自动重启";
            hint.Font = new Font("Microsoft YaHei UI", 8.5F);
            hint.ForeColor = Color.FromArgb(150, 150, 150);
            hint.AutoSize = true;
            hint.Location = new Point(26, 130);
            Controls.Add(hint);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 210);
            cancel.ForeColor = Color.FromArgb(90, 90, 90);
            cancel.Size = new Size(70, 28);
            cancel.Location = new Point(286, 126);
            cancel.Click += delegate
            {
                _cancelled = true;
                Close();
            };
            Controls.Add(cancel);

            Load += delegate { StartDownload(); };
        }

        private void StartDownload()
        {
            string downloadUrl = (_info.DownloadUrl ?? "").Trim();
            string altUrl = (_info.DownloadUrlAlt ?? "").Trim();
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    string downloaded = UpdateInstaller.DownloadPackage(downloadUrl, altUrl,
                        delegate(long read, long total) { PostProgress(read, total); });

                    if (_cancelled)
                    {
                        return;
                    }
                    Post(delegate(object s2) { _lblStatus.Text = "正在解包…"; }, null);

                    string newExe = UpdateInstaller.PrepareNewExe(downloaded);
                    if (_cancelled)
                    {
                        return;
                    }

                    Post(delegate(object s2) { _lblStatus.Text = "正在安装…"; }, null);
                    bool ok = UpdateInstaller.LaunchReplaceScript(newExe, System.Diagnostics.Process.GetCurrentProcess().Id);
                    if (!ok)
                    {
                        throw new Exception("启动替换脚本失败，请手动更新");
                    }

                    Post(delegate(object s2)
                    {
                        _lblStatus.Text = "更新完成，正在重启…";
                        // 稍候片刻让用户看到状态，再请求宿主退出
                        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                        t.Interval = 800;
                        t.Tick += delegate
                        {
                            t.Stop();
                            if (_onInstallReady != null)
                            {
                                _onInstallReady();
                            }
                        };
                        t.Start();
                    }, null);
                }
                catch (Exception ex)
                {
                    if (_cancelled)
                    {
                        return;
                    }
                    Log.WriteError("auto-update", ex);
                    Post(delegate(object s2)
                    {
                        MessageBox.Show("更新失败：" + ex.Message, "EyeCare20",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        Close();
                    }, null);
                }
            }, null);
        }

        private void PostProgress(long read, long total)
        {
            if (_cancelled)
            {
                throw new OperationCanceledException();
            }
            Post(delegate(object s)
            {
                if (total > 0)
                {
                    int pct = (int)(read * 100 / total);
                    if (pct > 100)
                    {
                        pct = 100;
                    }
                    _bar.Style = ProgressBarStyle.Continuous;
                    _bar.Value = pct;
                    _lblDetail.Text = FormatBytes(read) + " / " + FormatBytes(total);
                }
                else
                {
                    _bar.Style = ProgressBarStyle.Marquee;
                    _lblDetail.Text = FormatBytes(read);
                }
            }, null);
        }

        private void Post(SendOrPostCallback cb, object state)
        {
            if (_sync != null)
            {
                _sync.Post(cb, state);
            }
            else
            {
                cb(state);
            }
        }

        private static string FormatBytes(long b)
        {
            if (b >= 1024 * 1024)
            {
                return (b / 1024.0 / 1024.0).ToString("0.0") + " MB";
            }
            if (b >= 1024)
            {
                return (b / 1024.0).ToString("0.0") + " KB";
            }
            return b + " B";
        }
    }
}
