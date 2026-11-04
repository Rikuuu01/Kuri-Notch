using System;
using System.Windows.Interop;

namespace NotchWin.Interop
{
    /// <summary>
    /// Helper to apply tool-window styles and non-activating behavior to WPF windows.
    /// </summary>
    public static class WindowStyleHelper
    {
        public static void ApplyNotchWindowStyles(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            long exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            exStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

            // Ensure window is placed on top without activating it
            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW
            );
        }

        public static bool EnsureTopmost(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;

            // SWP_SHOWWINDOW is intentionally omitted: asserting z-order must not
            // resurrect a window that is currently hidden or collapsing.
            return NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER
            );
        }

        public static void SetNoActivate(IntPtr hwnd, bool noActivate)
        {
            if (hwnd == IntPtr.Zero) return;

            long exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            if (noActivate)
            {
                exStyle |= NativeMethods.WS_EX_NOACTIVATE;
            }
            else
            {
                exStyle &= ~NativeMethods.WS_EX_NOACTIVATE;
            }
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));
        }

        public static void AttachNoActivateHook(HwndSource source)
        {
            source.AddHook(WndProc);
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_MOUSEACTIVATE)
            {
                // Returns MA_NOACTIVATE so clicking the window does not take focus away from active application
                handled = true;
                return new IntPtr(NativeMethods.MA_NOACTIVATE);
            }

            return IntPtr.Zero;
        }
    }
}
