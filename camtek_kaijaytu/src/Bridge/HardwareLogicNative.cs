using System.Runtime.InteropServices;

namespace DMC.Bridge
{
    /// <summary>
    /// P/Invoke bridge to the native C++ HardwareLogic library (libHardwareLogic.so).
    /// Provides managed C# access to low-level hardware operations.
    /// String returns use IntPtr to prevent .NET from freeing C++-owned memory.
    /// </summary>
    internal static class HardwareLogicNative
    {
        private const string LibName = "libHardwareLogic";

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HW_Initialize();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HW_Shutdown();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HW_GetStatus();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr HW_GetStatusMessage();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int HW_ReadData(
            [MarshalAs(UnmanagedType.LPStr)] string elementId,
            [Out] byte[] buffer,
            int bufferSize);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int HW_WriteData(
            [MarshalAs(UnmanagedType.LPStr)] string elementId,
            [MarshalAs(UnmanagedType.LPStr)] string data,
            int dataSize);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HW_RunDiagnostics();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr HW_GetLastError();
    }
}
