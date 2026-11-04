using System;

namespace NotchWin.Models
{
    public class SearchResultLocation
    {
        public string Name { get; set; } = string.Empty;
        public string AdminRegion { get; set; } = string.Empty;
        public string Country { get; set; } = "Indonesia";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string TimeZone { get; set; } = "Asia/Jakarta";
        public double TimeZoneOffset { get; set; } = 7.0;

        public string FormattedDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(AdminRegion) && !string.IsNullOrEmpty(Country))
                    return $"{Name}, {AdminRegion}, {Country}";
                if (!string.IsNullOrEmpty(AdminRegion))
                    return $"{Name}, {AdminRegion}";
                if (!string.IsNullOrEmpty(Country))
                    return $"{Name}, {Country}";
                return Name;
            }
        }

        public string FormattedCoords => $"Lat: {Latitude:F2}, Lon: {Longitude:F2} (UTC{(TimeZoneOffset >= 0 ? "+" : "")}{TimeZoneOffset:0})";

        public override string ToString() => FormattedDisplay;
    }
}
