using System.Runtime.InteropServices;
using System.Text;

namespace DMC.Bridge
{
    /// <summary>
    /// Managed wrapper around the native HardwareLogic library.
    /// Provides a safe, C#-friendly API over the raw P/Invoke calls.
    /// </summary>
    public class HardwareBridge
    {
        private bool _initialized;

        public bool IsInitialized => _initialized;

        public void Initialize()
        {
            int result = HardwareLogicNative.HW_Initialize();
            if (result != 0)
            {
                throw new InvalidOperationException(
                    $"Hardware initialization failed: {GetLastError()}");
            }
            _initialized = true;
        }

        public void Shutdown()
        {
            int result = HardwareLogicNative.HW_Shutdown();
            if (result != 0)
            {
                throw new InvalidOperationException(
                    $"Hardware shutdown failed: {GetLastError()}");
            }
            _initialized = false;
        }

        public HardwareStatus GetStatus()
        {
            int status = HardwareLogicNative.HW_GetStatus();
            return status switch
            {
                0 => HardwareStatus.Uninitialized,
                1 => HardwareStatus.Ready,
                2 => HardwareStatus.Busy,
                _ => HardwareStatus.Error
            };
        }

        public string GetStatusMessage()
        {
            IntPtr ptr = HardwareLogicNative.HW_GetStatusMessage();
            return Marshal.PtrToStringAnsi(ptr) ?? "Unknown";
        }

        public string ReadData(string elementId)
        {
            EnsureInitialized();

            byte[] buffer = new byte[4096];
            int bytesRead = HardwareLogicNative.HW_ReadData(elementId, buffer, buffer.Length);

            if (bytesRead < 0)
            {
                throw new InvalidOperationException(
                    $"Read failed for element '{elementId}': {GetLastError()}");
            }

            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }

        public void WriteData(string elementId, string data)
        {
            EnsureInitialized();

            int result = HardwareLogicNative.HW_WriteData(elementId, data, data.Length);
            if (result != 0)
            {
                throw new InvalidOperationException(
                    $"Write failed for element '{elementId}': {GetLastError()}");
            }
        }

        public bool RunDiagnostics()
        {
            EnsureInitialized();
            return HardwareLogicNative.HW_RunDiagnostics() == 0;
        }

        public string GetLastError()
        {
            IntPtr ptr = HardwareLogicNative.HW_GetLastError();
            return Marshal.PtrToStringAnsi(ptr) ?? "";
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Hardware not initialized. Call Initialize() first.");
            }
        }
    }

    public enum HardwareStatus
    {
        Uninitialized = 0,
        Ready = 1,
        Busy = 2,
        Error = -1
    }
}
