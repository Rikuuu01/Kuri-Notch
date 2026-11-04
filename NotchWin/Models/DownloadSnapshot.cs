using System;

namespace NotchWin.Models
{
    public enum DownloadState
    {
        Downloading,
        Paused
    }

    public enum DownloadSource
    {
        Chromium,
        Firefox,
        Opera,
        Legacy,
        Generic
    }

    /// <summary>
    /// Read-only view of a single in-progress download as reported by the
    /// partial-file watcher. TotalBytes is filled for Chromium-family browsers
    /// from their History database; other sources leave it null and only
    /// expose bytes received + measured speed.
    /// </summary>
    public sealed record DownloadSnapshot(
        string PartialPath,
        string FileName,
        string DirectoryPath,
        DownloadSource Source,
        DownloadState State,
        long BytesReceived,
        double BytesPerSecond,
        long? TotalBytes,
        DateTime StartedAt,
        DateTime LastGrowthAt,
        bool IsStale);

    public sealed class DownloadCompletedEventArgs : EventArgs
    {
        public DownloadCompletedEventArgs(string fileName, string directoryPath)
        {
            FileName = fileName;
            DirectoryPath = directoryPath;
        }

        public string FileName { get; }
        public string DirectoryPath { get; }
    }
}
