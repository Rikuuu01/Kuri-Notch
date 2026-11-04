using System;
using System.Threading.Tasks;

namespace NotchWin.Services
{
    public interface IBrightnessService : IDisposable
    {
        int CurrentBrightness { get; }
        bool IsSupported { get; }

        event EventHandler<int>? BrightnessChanged;

        Task InitializeAsync();
        void SetBrightness(int percent);
    }
}
