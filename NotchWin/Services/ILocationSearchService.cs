using System.Collections.Generic;
using System.Threading.Tasks;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface ILocationSearchService
    {
        IReadOnlyList<SearchResultLocation> OfflinePresets { get; }
        Task<List<SearchResultLocation>> SearchAsync(string query);
    }
}
