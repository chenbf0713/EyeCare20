using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;

namespace EyeCare20
{
    [DataContract]
    public class UpdateInfo
    {
        [DataMember(Name = "version")]
        public string Version;

        [DataMember(Name = "downloadUrl")]
        public string DownloadUrl;

        /// <summary>备用下载地址（可选）：主地址失败时使用，如 GitHub Releases（主地址放 Gitee Releases）。</summary>
        [DataMember(Name = "downloadUrlAlt")]
        public string DownloadUrlAlt;

        [DataMember(Name = "notes")]
        public string Notes;
    }

    /// <summary>
    /// 在线检查更新：GET 一个 JSON（version/downloadUrl/downloadUrlAlt/notes），与当前程序集版本比对。
    /// 支持多源回退：UpdateUrl 为空时用内置源（Gitee 优先 → GitHub 回退）；
    /// UpdateUrl 可用 "|" 分隔多个地址，按顺序尝试第一个成功的。
    /// </summary>
    internal static class UpdateChecker
    {
        /// <summary>按顺序尝试多个 update.json 地址，返回第一个成功结果；全部失败返回 null。</summary>
        public static UpdateInfo CheckAny(string[] urls)
        {
            if (urls == null)
            {
                return null;
            }
            for (int i = 0; i < urls.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(urls[i]))
                {
                    continue;
                }
                UpdateInfo info = Check(urls[i]);
                if (info != null)
                {
                    return info;
                }
            }
            return null;
        }

        /// <summary>同步检查单个地址（调用方负责放后台线程）。失败返回 null。</summary>
        public static UpdateInfo Check(string url)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = 8000;
                req.ReadWriteTimeout = 8000;
                req.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                req.UserAgent = "EyeCare20/" + CurrentVersion().ToString();

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream stream = resp.GetResponseStream())
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buffer = new byte[4096];
                    int read;
                    while (stream != null && (read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, read);
                    }
                    byte[] bytes = ms.ToArray();
                    // 兼容 UTF-8 BOM
                    int offset = (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0;
                    using (MemoryStream json = new MemoryStream(bytes, offset, bytes.Length - offset, false))
                    {
                        DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(UpdateInfo));
                        UpdateInfo info = ser.ReadObject(json) as UpdateInfo;
                        if (info != null && !string.IsNullOrWhiteSpace(info.Version))
                        {
                            return info;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("update-check " + url, ex);
            }
            return null;
        }

        public static Version CurrentVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        }

        /// <summary>远端版本是否比当前版本新。</summary>
        public static bool IsNewer(UpdateInfo info)
        {
            Version remote;
            if (!Version.TryParse(info.Version, out remote))
            {
                return false;
            }
            return remote > CurrentVersion();
        }
    }

    /// <summary>发现新版本的提示小窗（非模态，不阻塞后台）。</summary>
    internal sealed class UpdateNoticeForm : Form
    {
        /// <summary>用户点击"立即更新"：请求宿主执行自动更新（下载→替换→重启）。</summary>
        public event Action<UpdateInfo> InstallRequested;

        public UpdateNoticeForm(UpdateInfo info)
        {
            Text = I18n.T("发现新版本");
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9.5F);
            ClientSize = new Size(380, 210);

            Label title = new Label();
            title.Text = I18n.T("发现新版本 v") + info.Version;
            title.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(4, 138, 74);
            title.AutoSize = true;
            title.Location = new Point(24, 20);
            Controls.Add(title);

            Label current = new Label();
            current.Text = I18n.T("当前版本 v") + UpdateChecker.CurrentVersion().ToString();
            current.ForeColor = Color.FromArgb(140, 140, 140);
            current.AutoSize = true;
            current.Location = new Point(26, 52);
            Controls.Add(current);

            Label notes = new Label();
            notes.Text = string.IsNullOrWhiteSpace(info.Notes) ? I18n.T("（暂无更新说明）") : info.Notes;
            notes.ForeColor = Color.FromArgb(60, 60, 60);
            notes.Location = new Point(26, 80);
            notes.Size = new Size(330, 56);
            Controls.Add(notes);

            Button install = new Button();
            install.Text = I18n.T("立即更新");
            install.FlatStyle = FlatStyle.Flat;
            install.FlatAppearance.BorderColor = Color.FromArgb(4, 138, 74);
            install.ForeColor = Color.White;
            install.BackColor = Color.FromArgb(4, 138, 74);
            install.Cursor = Cursors.Hand;
            install.Size = new Size(110, 34);
            install.Location = new Point(26, 152);
            install.Click += delegate
            {
                Action<UpdateInfo> h = InstallRequested;
                if (h != null)
                {
                    h(info);
                }
                Close();
            };
            Controls.Add(install);

            Button later = new Button();
            later.Text = I18n.T("以后再说");
            later.FlatStyle = FlatStyle.Flat;
            later.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 210);
            later.ForeColor = Color.FromArgb(90, 90, 90);
            later.Size = new Size(90, 34);
            later.Location = new Point(150, 152);
            later.Click += delegate { Close(); };
            Controls.Add(later);
        }
    }
}
