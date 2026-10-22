using System;
using System.Threading;
using System.Threading.Tasks;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface ILyricsService
    {
        Task<LyricsResult> GetLyricsAsync(string title, string artist, TimeSpan duration, CancellationToken ct = default);
    }
}
