using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface ILrcRedLyricsService
    {
        Task<List<LyricsCandidate>> GetCandidatesAsync(string cleanTitle, string cleanArtist, TimeSpan duration, CancellationToken ct = default);
    }
}
