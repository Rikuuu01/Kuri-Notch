using System;
using System.Collections.Generic;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface IDownloadMonitorService : IDisposable
    {
        /// <summary>Snapshot of every tracked download, including stale paused files.</summary>
        IReadOnlyList<DownloadSnapshot> ActiveDownloads { get; }

        event EventHandler? DownloadsChanged;

        /// <summary>Raised when a partial file is renamed to its final name.</summary>
        event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;

        void Start();
        void Stop();
        void Suspend();
        void Resume();
    }
}
