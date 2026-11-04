using System;

namespace NotchWin.Services
{
    public interface IFullscreenDetector : IDisposable
    {
        bool IsFullscreenActive { get; }
        event EventHandler<bool>? FullscreenChanged;
        void Start();
        void Stop();
    }
}
