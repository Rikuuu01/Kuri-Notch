using System;

namespace NotchWin.Services
{
    public interface IPrivacyService : IDisposable
    {
        bool IsCameraInUse { get; }
        bool IsMicrophoneInUse { get; }
        event EventHandler? PrivacyStateChanged;
        void Start();
        void Stop();
        void Pause();
        void Resume();
    }
}
