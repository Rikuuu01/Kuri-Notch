using System;
using System.Threading.Tasks;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface IMediaService
    {
        event EventHandler<MediaTrackInfo>? MediaTrackChanged;
        event EventHandler<bool>? PlaybackStateChanged;

        MediaTrackInfo CurrentTrack { get; }

        Task InitializeAsync();
        Task TogglePlayPauseAsync();
        Task SkipNextAsync();
        Task SkipPreviousAsync();
    }
}
