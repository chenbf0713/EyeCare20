using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EyeCare20
{
    /// <summary>
    /// 全屏检测（视频/游戏勿扰）：
    /// 1) SHQueryUserNotificationState —— 独占 D3D 全屏 / 演示模式；
    /// 2) 前台窗口覆盖整个屏幕 且 无标题栏样式（浏览器 F11、无边框游戏窗口等）。
    /// 最大化但带标题栏的普通窗口不算全屏，不打扰正常办公。
    /// </summary>
    internal static class FullScreenDetector
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_STYLE = -16;
        private const long WS_CAPTION = 0x00C00000L;

        private enum QUNS
        {
            QUNS_NOT_PRESENT = 1,
            QUNS_BUSY = 2,
            QUNS_RUNNING_D3D_FULL_SCREEN = 3,
            QUNS_PRESENTATION_MODE = 4,
            QUNS_ACCEPTS_NOTIFICATIONS = 5,
            QUNS_QUIET_TIME = 6,
            QUNS_APP = 7
        }

        [DllImport("shell32.dll")]
        private static extern int SHQueryUserNotificationState(out QUNS state);

        public static bool IsFullScreen()
        {
            try
            {
                QUNS state;
                if (SHQueryUserNotificationState(out state) == 0)
                {
                    if (state == QUNS.QUNS_RUNNING_D3D_FULL_SCREEN || state == QUNS.QUNS_PRESENTATION_MODE)
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
            }

            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero || hwnd == GetShellWindow())
                {
                    return false;
                }
                RECT r;
                if (!GetWindowRect(hwnd, out r))
                {
                    return false;
                }
                Screen s = Screen.FromHandle(hwnd);
                bool covers = r.Left <= s.Bounds.Left && r.Top <= s.Bounds.Top
                           && r.Right >= s.Bounds.Right && r.Bottom >= s.Bounds.Bottom;
                if (!covers)
                {
                    return false;
                }
                long style = (uint)GetWindowLong(hwnd, GWL_STYLE);
                bool hasCaption = (style & WS_CAPTION) != 0;
                return !hasCaption;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
