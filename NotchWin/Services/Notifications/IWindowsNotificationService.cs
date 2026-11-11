using System;

namespace NotchWin.Services.Notifications
{
    public interface IWindowsNotificationService : IDisposable
    {
        event EventHandler<WindowsNotificationModel>? NotificationReceived;
        bool IsRunning { get; }
        void Start();
        void Stop();
    }
}
