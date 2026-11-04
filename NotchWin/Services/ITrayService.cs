using System;

namespace NotchWin.Services
{
    public interface ITrayService : IDisposable
    {
        void Initialize(IntPtr windowHandle);
        void HandleTrayMessage(IntPtr wParam, IntPtr lParam);
        event EventHandler? OpenSettingsRequested;
        event EventHandler? ToggleVisibilityRequested;
        event EventHandler? ExitRequested;
    }
}
