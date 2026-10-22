using System;
using System.Runtime.InteropServices;

namespace NotchWin.Interop
{
    /// <summary>
    /// Win32 API interop declarations for window styles, positioning, and focus management.
    /// Note: 64-bit user32.dll exports GetWindowLongPtrW and SetWindowLongPtrW.
    /// </summary>
    public static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;

        // Window extended styles
        public const int WS_EX_TOOLWINDOW = 0x00000080; // Hides window from Alt+Tab and taskbar
        public const int WS_EX_NOACTIVATE = 0x08000000; // Prevents window from stealing foreground focus when clicked

        // Window messages
        public const int WM_MOUSEACTIVATE = 0x0021;
        public const int MA_NOACTIVATE = 3; // Do not activate window, eat neither mouse click

        public const int WM_NCHITTEST = 0x0084;
        public const int HTTRANSPARENT = -1;
        public const int HTCLIENT = 1;

        // SetWindowPos flags
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }
    }
}
