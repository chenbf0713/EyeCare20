using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace EyeCare20
{
    /// <summary>
    /// 全自动更新安装器：下载 → 校验/解包 → 备份当前版 → 外部脚本替换并重启。
    /// Windows 下运行中的 exe 无法覆盖自身，采用经典方案：
    /// 由 cmd 脚本等待旧进程退出后 move 替换、启动新版、清理临时文件；
    /// 替换失败时用备份回滚，保证任何时候磁盘上都有可用 exe。
    /// </summary>
    internal static class UpdateInstaller
    {
        private static readonly string DownloadPath = Path.Combine(Path.GetTempPath(), "eyecare20_download.bin");
        private static readonly string NewExePath = Path.Combine(Path.GetTempPath(), "eyecare20_new.exe");
        private static readonly string ScriptPath = Path.Combine(Path.GetTempPath(), "eyecare20_replace.cmd");

        /// <summary>下载更新包到临时文件；主地址失败自动尝试备用地址（Gitee↔GitHub）。progress(read, total) 在下载线程回调，total 可能为 -1。</summary>
        public static string DownloadPackage(string url, string altUrl, Action<long, long> progress)
        {
            if (!string.IsNullOrWhiteSpace(altUrl))
            {
                try
                {
                    return DownloadTo(url, progress);
                }
                catch (Exception ex)
                {
                    Log.WriteError("download-primary-failed, trying alt", ex);
                    return DownloadTo(altUrl, progress);
                }
            }
            return DownloadTo(url, progress);
        }

        private static string DownloadTo(string url, Action<long, long> progress)
        {
            if (!IsAllowedUrl(url))
            {
                throw new Exception("更新地址必须为 https（本机 127.0.0.1 除外）");
            }
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 15000;
            req.ReadWriteTimeout = 30000;
            req.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            req.UserAgent = "EyeCare20/" + UpdateChecker.CurrentVersion().ToString();

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream rs = resp.GetResponseStream())
            using (FileStream fs = new FileStream(DownloadPath, FileMode.Create, FileAccess.Write))
            {
                long total = resp.ContentLength;
                byte[] buf = new byte[8192];
                long read = 0;
                int n;
                while ((n = rs.Read(buf, 0, buf.Length)) > 0)
                {
                    fs.Write(buf, 0, n);
                    read += n;
                    if (progress != null)
                    {
                        progress(read, total);
                    }
                }
            }
            if (!File.Exists(DownloadPath) || new FileInfo(DownloadPath).Length == 0)
            {
                throw new Exception("下载内容为空");
            }
            Log.Write("update package downloaded: " + new FileInfo(DownloadPath).Length + " bytes");
            return DownloadPath;
        }

        /// <summary>解包更新包：直接是 exe 则直接用；是 zip 则解出其中的 exe。返回新 exe 临时路径。</summary>
        public static string PrepareNewExe(string downloadedPath)
        {
            byte[] head = new byte[2];
            using (FileStream fs = File.OpenRead(downloadedPath))
            {
                if (fs.Read(head, 0, 2) < 2)
                {
                    throw new Exception("更新包内容不完整");
                }
            }

            if (head[0] == (byte)'M' && head[1] == (byte)'Z')
            {
                File.Copy(downloadedPath, NewExePath, true);
                File.Delete(downloadedPath);
                Log.Write("update package is a plain exe");
                return NewExePath;
            }

            if (head[0] == (byte)'P' && head[1] == (byte)'K')
            {
                using (ZipArchive zip = ZipFile.OpenRead(downloadedPath))
                {
                    ZipArchiveEntry entry = null;
                    foreach (ZipArchiveEntry e in zip.Entries)
                    {
                        if (string.Equals(e.Name, "EyeCare20.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            entry = e;
                            break;
                        }
                    }
                    if (entry == null)
                    {
                        foreach (ZipArchiveEntry e in zip.Entries)
                        {
                            if (e.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                entry = e;
                                break;
                            }
                        }
                    }
                    if (entry == null)
                    {
                        throw new Exception("更新包内未找到 exe 文件");
                    }
                    entry.ExtractToFile(NewExePath, true);
                }
                File.Delete(downloadedPath);
                VerifyPe(NewExePath);
                Log.Write("update package unzipped");
                return NewExePath;
            }

            throw new Exception("更新包不是有效的 exe 或 zip 文件");
        }

        private static void VerifyPe(string exePath)
        {
            byte[] head = new byte[2];
            using (FileStream fs = File.OpenRead(exePath))
            {
                if (fs.Read(head, 0, 2) < 2 || head[0] != (byte)'M' || head[1] != (byte)'Z')
                {
                    throw new Exception("解包结果不是有效的 exe 文件");
                }
            }
        }

        /// <summary>备份当前 exe，生成并启动替换脚本（等待本进程退出 → 替换 → 重启 → 清理）。</summary>
        public static bool LaunchReplaceScript(string newExePath, int currentProcessId)
        {
            try
            {
                string target = Application.ExecutablePath;
                string bak = target + ".bak";
                File.Copy(target, bak, true);   // 备份当前版本（运行中的 exe 可读不可写，读没问题）

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("setlocal");
                sb.AppendLine("set \"TARGET=" + target + "\"");
                sb.AppendLine("set \"NEW=" + newExePath + "\"");
                sb.AppendLine("set \"BAK=" + bak + "\"");
                sb.AppendLine("set PID=" + currentProcessId);
                sb.AppendLine(":WAITLOOP");
                sb.AppendLine("tasklist /fi \"PID eq %PID%\" 2>nul | find \"%PID%\" >nul");
                sb.AppendLine("if not errorlevel 1 (");
                sb.AppendLine("  timeout /t 1 /nobreak >nul");
                sb.AppendLine("  goto WAITLOOP");
                sb.AppendLine(")");
                sb.AppendLine("set /a TRY=0");
                sb.AppendLine(":MOVELOOP");
                sb.AppendLine("move /y \"%NEW%\" \"%TARGET%\" >nul 2>&1");
                sb.AppendLine("if not exist \"%NEW%\" goto MOVED");
                sb.AppendLine("timeout /t 1 /nobreak >nul");
                sb.AppendLine("set /a TRY+=1");
                sb.AppendLine("if %TRY% LSS 15 goto MOVELOOP");
                sb.AppendLine("goto RESTORE");
                sb.AppendLine(":MOVED");
                sb.AppendLine("start \"\" \"%TARGET%\"");
                sb.AppendLine("goto CLEAN");
                sb.AppendLine(":RESTORE");
                sb.AppendLine("copy /y \"%BAK%\" \"%TARGET%\" >nul 2>&1");
                sb.AppendLine("goto CLEAN");
                sb.AppendLine(":CLEAN");
                sb.AppendLine("if exist \"%NEW%\" del /q \"%NEW%\" >nul 2>&1");
                sb.AppendLine("del /q \"%~f0\" >nul 2>&1");
                sb.AppendLine("exit");
                // ANSI 编码：cmd 原生兼容，中文路径也正确
                File.WriteAllText(ScriptPath, sb.ToString(), Encoding.Default);

                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + ScriptPath + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process.Start(psi);
                Log.Write("replace script launched (pid=" + currentProcessId + "): " + ScriptPath);
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteError("launch-replace-script", ex);
                return false;
            }
        }

        /// <summary>更新地址安全校验：必须 https；仅允许本机 127.0.0.1/localhost 走 http（开发/测试）。</summary>
        private static bool IsAllowedUrl(string url)
        {
            try
            {
                Uri u = new Uri(url);
                if (u.Scheme == Uri.UriSchemeHttps)
                {
                    return true;
                }
                if (u.Scheme == Uri.UriSchemeHttp)
                {
                    string h = u.Host;
                    return h == "127.0.0.1" || h == "localhost" || h == "::1";
                }
            }
            catch (Exception)
            {
            }
            return false;
        }
    }
}
