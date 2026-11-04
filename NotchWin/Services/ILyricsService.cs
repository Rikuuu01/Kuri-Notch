using System;
using System.Threading;
using System.Threading.Tasks;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface ILyricsService
    {
        Task<LyricsFetchResult> GetLyricsCandidatesAsync(string rawTitle, string rawArtist, TimeSpan duration, CancellationToken ct = default);
    }
}
