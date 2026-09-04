using System;
using System.IO;

namespace EyeCare20
{
    /// <summary>极简文件日志：%APPDATA%\EyeCare20\app.log，用于诊断托盘应用问题。</summary>
    internal static class Log
    {
        private static readonly object Gate = new object();
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EyeCare20", "app.log");

        public static void Write(string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine;
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(LogPath, line);
                }
            }
            catch (Exception)
            {
            }
            try
            {
                // 兜底路径：主日志不可写时仍能诊断
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "eyecare20.log"), line);
            }
            catch (Exception)
            {
            }
        }

        public static void WriteError(string context, Exception ex)
        {
            Write(context + " :: " + (ex == null ? "null" : ex.ToString()));
        }
    }
}
