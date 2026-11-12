using System;

namespace NotchWin.Services
{
    public interface IKeyboardLockService : IDisposable
    {
        bool IsCapsLockOn { get; }
        bool IsNumLockOn { get; }

        event EventHandler<bool>? CapsLockChanged;
        event EventHandler<bool>? NumLockChanged;

        void Start();
        void Stop();
    }
}
