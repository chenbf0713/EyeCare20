using System;
using System.Threading;
using System.Windows.Forms;

namespace EyeCare20
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Log.Write("main entered");
            bool createdNew;
            Mutex mutex = new Mutex(true, "Global\\EyeCare20.SingleInstance", out createdNew);
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
                try { mutex.ReleaseMutex(); }
                catch (Exception) { }
                GC.KeepAlive(mutex);
            }
        }
    }
}
