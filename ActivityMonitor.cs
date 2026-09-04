using System;
using System.Runtime.InteropServices;

namespace EyeCare20
{
    /// <summary>
    /// 高级模式活动检测：
    /// 1) 鼠标/键盘 —— GetLastInputInfo（系统级，距上次输入 ≤ 60 秒视为活跃）；
    /// 2) 音频输出 —— Core Audio 默认渲染设备峰值表（看视频/听歌时即使不操作也算使用中）。
    /// 两者任一活跃即视为"正在使用电脑"。
    /// </summary>
    internal sealed class ActivityMonitor : IDisposable
    {
        private const uint IdleThresholdSeconds = 60;   // 超过此秒数无鼠标/键盘输入 = 离开
        private const float AudioPeakThreshold = 0.01f; // 音频峰值高于此值 = 有音频输出
        private const double AudioPollSeconds = 2.0;    // 音频采样间隔（秒）
        private const double MeterRefreshMinutes = 5.0; // 定期重建峰值表，兼容默认设备切换

        private static readonly Guid IidAudioMeterInformation = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");
        private const uint ClsctxInprocServer = 1;

        // ---------- 鼠标 / 键盘 ----------

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        /// <summary>距上次鼠标/键盘输入的空闲秒数（系统级）。</summary>
        public static uint GetIdleSeconds()
        {
            LASTINPUTINFO info = new LASTINPUTINFO();
            info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref info))
            {
                return 0;
            }
            // uint 算术保证 TickCount 回绕时结果仍正确
            uint idle = ((uint)Environment.TickCount - info.dwTime) / 1000u;
            return idle;
        }

        // ---------- Core Audio 峰值表（音频输出检测） ----------

        private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
        private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject
        {
        }

        // 只需声明用到的前两个方法（vtable 顺序保持一致）
        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask, out IntPtr ppDevices);

            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams,
                [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [ComImport]
        [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioMeterInformation
        {
            [PreserveSig]
            int GetPeakValue(out float pfPeak);
        }

        private IAudioMeterInformation _meter;
        private DateTime _meterAcquiredAt = DateTime.MinValue;
        private DateTime _lastAudioPoll = DateTime.MinValue;
        private bool _lastAudioActive;

        /// <summary>综合活跃判定：鼠标/键盘活跃 或 音频输出活跃。</summary>
        public bool IsUserActive()
        {
            bool inputActive = GetIdleSeconds() <= IdleThresholdSeconds;

            if ((DateTime.Now - _lastAudioPoll).TotalSeconds >= AudioPollSeconds)
            {
                _lastAudioPoll = DateTime.Now;
                PollAudio();
            }

            return inputActive || _lastAudioActive;
        }

        private bool PollAudio()
        {
            try
            {
                if (_meter == null || (DateTime.Now - _meterAcquiredAt).TotalMinutes >= MeterRefreshMinutes)
                {
                    ReleaseMeter();
                    if (!AcquireMeter())
                    {
                        return false;
                    }
                }

                float peak;
                int hr = _meter.GetPeakValue(out peak);
                if (hr != 0)
                {
                    // 设备可能已变更，下次重新获取
                    ReleaseMeter();
                    return false;
                }
                bool nowActive = peak > AudioPeakThreshold;
                if (nowActive != _lastAudioActive)
                {
                    Log.Write("audio change: active=" + nowActive + " peak=" + peak.ToString("0.000"));
                }
                _lastAudioActive = nowActive;
                return _lastAudioActive;
            }
            catch (Exception)
            {
                ReleaseMeter();
                return false;
            }
        }

        private bool AcquireMeter()
        {
            IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
            IMMDevice device;
            int hr = enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out device);
            if (hr != 0 || device == null)
            {
                return false;
            }
            object obj;
            Guid iid = IidAudioMeterInformation;
            hr = device.Activate(ref iid, ClsctxInprocServer, IntPtr.Zero, out obj);
            if (hr != 0 || obj == null)
            {
                return false;
            }
            _meter = (IAudioMeterInformation)obj;
            _meterAcquiredAt = DateTime.Now;
            return true;
        }

        private void ReleaseMeter()
        {
            if (_meter != null)
            {
                try { Marshal.ReleaseComObject(_meter); }
                catch (Exception) { }
                _meter = null;
            }
        }

        public void Dispose()
        {
            ReleaseMeter();
        }
    }
}
