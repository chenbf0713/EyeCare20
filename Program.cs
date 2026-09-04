using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace EyeCare20
{
    internal static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        private static void Main()
        {
            Log.Write("main entered");
            bool createdNew;
            _mutex = new Mutex(true, "Global\\EyeCare20.SingleInstance", out createdNew);
            Log.Write("mutex created, createdNew=" + createdNew);
            if (!createdNew)
            {
                // 已有实例在运行，直接退出
                return;
            }
            try
            {
                Application.EnableVisualStyles();
                Log.Write("visual styles ok");
                Application.SetCompatibleTextRenderingDefault(false);
                Log.Write("compatible text ok");
                Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs ex)
                {
                    Log.WriteError("UI-ThreadException", ex.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs ex)
                {
                    Log.WriteError("AppDomain-Unhandled", ex.ExceptionObject as Exception);
                };
                Log.Write("before new TrayContext");
                // WinForms 的 SynchronizationContext 在创建第一个 Control 时才自动安装，
                // 而 TrayContext 在 Application.Run 之前构造，这里显式安装，
                // 保证后台线程结果（更新检查/电源事件）能封送回 UI 线程。
                System.Threading.SynchronizationContext.SetSynchronizationContext(
                    new System.Windows.Forms.WindowsFormsSynchronizationContext());
                Application.Run(new TrayContext());
                Log.Write("run returned");
            }
            catch (Exception ex)
            {
                Log.WriteError("Main-Fatal", ex);
                throw;
            }
            finally
            {
                try { _mutex.ReleaseMutex(); }
                catch (Exception) { }
                GC.KeepAlive(_mutex);
            }
        }

        /// <summary>重启应用：释放单实例锁，启动新进程，退出当前进程。</summary>
        public static void Restart()
        {
            try
            {
                // 先释放单实例 Mutex，避免新进程因获取不到锁而立即退出
                try { _mutex.ReleaseMutex(); }
                catch (Exception) { }
                // 启动新实例
                Process.Start(new ProcessStartInfo(Application.ExecutablePath)
                {
                    UseShellExecute = false,
                });
                Log.Write("restart: new instance launched");
            }
            catch (Exception ex)
            {
                Log.WriteError("restart-launch", ex);
            }
            // 退出当前实例
            Application.Exit();
        }
    }
}
