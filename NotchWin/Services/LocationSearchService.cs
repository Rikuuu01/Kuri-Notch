using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class LocationSearchService : ILocationSearchService
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

        private static readonly List<SearchResultLocation> _offlineCities = new()
        {
            new SearchResultLocation { Name = "Jakarta", AdminRegion = "DKI Jakarta", Latitude = -6.2088, Longitude = 106.8456, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Bandung", AdminRegion = "Jawa Barat", Latitude = -6.9175, Longitude = 107.6191, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Surabaya", AdminRegion = "Jawa Timur", Latitude = -7.2575, Longitude = 112.7521, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Semarang", AdminRegion = "Jawa Tengah", Latitude = -6.9667, Longitude = 110.4167, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Yogyakarta", AdminRegion = "DI Yogyakarta", Latitude = -7.7956, Longitude = 110.3695, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Medan", AdminRegion = "Sumatera Utara", Latitude = 3.5952, Longitude = 98.6722, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Palembang", AdminRegion = "Sumatera Selatan", Latitude = -2.9761, Longitude = 104.7754, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Padang", AdminRegion = "Sumatera Barat", Latitude = -0.9471, Longitude = 100.4172, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Duri", AdminRegion = "Riau", Latitude = 1.2761, Longitude = 101.1946, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Pekanbaru", AdminRegion = "Riau", Latitude = 0.5071, Longitude = 101.4478, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Banda Aceh", AdminRegion = "Aceh", Latitude = 5.5483, Longitude = 95.3238, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Denpasar", AdminRegion = "Bali", Latitude = -8.6705, Longitude = 115.2126, TimeZoneOffset = 8.0 },
            new SearchResultLocation { Name = "Makassar", AdminRegion = "Sulawesi Selatan", Latitude = -5.1477, Longitude = 119.4327, TimeZoneOffset = 8.0 },
            new SearchResultLocation { Name = "Banjarmasin", AdminRegion = "Kalimantan Selatan", Latitude = -3.3194, Longitude = 114.5908, TimeZoneOffset = 8.0 },
            new SearchResultLocation { Name = "Balikpapan", AdminRegion = "Kalimantan Timur", Latitude = -1.2379, Longitude = 116.8529, TimeZoneOffset = 8.0 },
            new SearchResultLocation { Name = "Samarinda", AdminRegion = "Kalimantan Timur", Latitude = -0.5022, Longitude = 117.1536, TimeZoneOffset = 8.0 },
            new SearchResultLocation { Name = "Pontianak", AdminRegion = "Kalimantan Barat", Latitude = -0.0263, Longitude = 109.3425, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Manado", AdminRegion = "Sulawesi Utara", Latitude = 1.4748, Longitude = 124.8421, TimeZoneOffset = 8.0 },
            new SearchResultLocation { Name = "Mataram", AdminRegion = "Nusa Tenggara Barat", Latitude = -8.5833, Longitude = 116.1167, TimeZoneOffset = 8.0 },
            new SearchResultLocation { Name = "Ambon", AdminRegion = "Maluku", Latitude = -3.6954, Longitude = 128.1814, TimeZoneOffset = 9.0 },
            new SearchResultLocation { Name = "Jayapura", AdminRegion = "Papua", Latitude = -2.5489, Longitude = 140.7181, TimeZoneOffset = 9.0 },
            new SearchResultLocation { Name = "Malang", AdminRegion = "Jawa Timur", Latitude = -7.9666, Longitude = 112.6326, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Bogor", AdminRegion = "Jawa Barat", Latitude = -6.5971, Longitude = 106.8060, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Depok", AdminRegion = "Jawa Barat", Latitude = -6.4025, Longitude = 106.7942, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Bekasi", AdminRegion = "Jawa Barat", Latitude = -6.2383, Longitude = 106.9756, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Tangerang", AdminRegion = "Banten", Latitude = -6.1783, Longitude = 106.6319, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Tangerang Selatan", AdminRegion = "Banten", Latitude = -6.2887, Longitude = 106.7179, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Surakarta (Solo)", AdminRegion = "Jawa Tengah", Latitude = -7.5755, Longitude = 110.8243, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Cirebon", AdminRegion = "Jawa Barat", Latitude = -6.7320, Longitude = 108.5523, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Cimahi", AdminRegion = "Jawa Barat", Latitude = -6.8722, Longitude = 107.5425, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Banyuwangi", AdminRegion = "Jawa Timur", Latitude = -8.2325, Longitude = 114.3575, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Jember", AdminRegion = "Jawa Timur", Latitude = -8.1724, Longitude = 113.7007, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Batam", AdminRegion = "Kepulauan Riau", Latitude = 1.1296, Longitude = 104.0530, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Bandar Lampung", AdminRegion = "Lampung", Latitude = -5.4500, Longitude = 105.2667, TimeZoneOffset = 7.0 },
            new SearchResultLocation { Name = "Kupang", AdminRegion = "Nusa Tenggara Timur", Latitude = -10.1772, Longitude = 123.6070, TimeZoneOffset = 8.0 }
        };

        public IReadOnlyList<SearchResultLocation> OfflinePresets => _offlineCities;

        public async Task<List<SearchResultLocation>> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<SearchResultLocation>(_offlineCities.Take(12));

            string q = query.Trim().ToLowerInvariant();
            var results = new List<SearchResultLocation>();

            // 1. Search local presets first
            var localMatches = _offlineCities.Where(c =>
                c.Name.ToLowerInvariant().Contains(q) ||
                c.AdminRegion.ToLowerInvariant().Contains(q)
            ).ToList();

            results.AddRange(localMatches);

            // 2. Query Open-Meteo Geocoding API for exact coordinates worldwide & in Indonesia
            try
            {
                string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query.Trim())}&count=10&language=id&format=json";
                using var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("results", out var resArray) && resArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in resArray.EnumerateArray())
                        {
                            string name = item.TryGetProperty("name", out var pName) ? pName.GetString() ?? "" : "";
                            string admin1 = item.TryGetProperty("admin1", out var pAdmin) ? pAdmin.GetString() ?? "" : "";
                            string country = item.TryGetProperty("country", out var pCountry) ? pCountry.GetString() ?? "" : "";
                            double lat = item.TryGetProperty("latitude", out var pLat) ? pLat.GetDouble() : 0.0;
                            double lon = item.TryGetProperty("longitude", out var pLon) ? pLon.GetDouble() : 0.0;
                            string tz = item.TryGetProperty("timezone", out var pTz) ? pTz.GetString() ?? "" : "";

                            double tzOffset = DeriveTimezoneOffset(tz, lon);

                            // Avoid duplicate coordinates
                            if (!results.Any(r => Math.Abs(r.Latitude - lat) < 0.02 && Math.Abs(r.Longitude - lon) < 0.02))
                            {
                                results.Add(new SearchResultLocation
                                {
                                    Name = name,
                                    AdminRegion = admin1,
                                    Country = country,
                                    Latitude = Math.Round(lat, 4),
                                    Longitude = Math.Round(lon, 4),
                                    TimeZone = tz,
                                    TimeZoneOffset = tzOffset
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to query Open-Meteo geocoding API for query '{Query}'", query);
            }

            return results;
        }

        private static double DeriveTimezoneOffset(string timezone, double longitude)
        {
            if (timezone.Contains("Jakarta", StringComparison.OrdinalIgnoreCase) ||
                timezone.Contains("Pontianak", StringComparison.OrdinalIgnoreCase))
                return 7.0;

            if (timezone.Contains("Makassar", StringComparison.OrdinalIgnoreCase) ||
                timezone.Contains("Ujung_Pandang", StringComparison.OrdinalIgnoreCase))
                return 8.0;

            if (timezone.Contains("Jayapura", StringComparison.OrdinalIgnoreCase))
                return 9.0;

            try
            {
                if (!string.IsNullOrEmpty(timezone))
                {
                    var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                    return tzInfo.GetUtcOffset(DateTime.Now).TotalHours;
                }
            }
            catch { }

            // Estimate from longitude (15 degrees per hour)
            return Math.Round(longitude / 15.0);
        }
    }
}
