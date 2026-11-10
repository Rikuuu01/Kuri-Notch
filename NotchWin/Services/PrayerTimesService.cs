using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class PrayerTimesService : IPrayerTimesService, IDisposable
    {
        public static readonly IReadOnlyList<CityLocation> PresetCities = new List<CityLocation>
        {
            new CityLocation { Name = "Jakarta", Latitude = -6.2088, Longitude = 106.8456, TimeZoneOffset = 7.0 },
            new CityLocation { Name = "Bandung", Latitude = -6.9175, Longitude = 107.6191, TimeZoneOffset = 7.0 },
            new CityLocation { Name = "Surabaya", Latitude = -7.2575, Longitude = 112.7521, TimeZoneOffset = 7.0 },
            new CityLocation { Name = "Semarang", Latitude = -6.9667, Longitude = 110.4167, TimeZoneOffset = 7.0 },
            new CityLocation { Name = "Yogyakarta", Latitude = -7.7956, Longitude = 110.3695, TimeZoneOffset = 7.0 },
            new CityLocation { Name = "Medan", Latitude = 3.5952, Longitude = 98.6722, TimeZoneOffset = 7.0 },
            new CityLocation { Name = "Palembang", Latitude = -2.9761, Longitude = 104.7754, TimeZoneOffset = 7.0 },
            new CityLocation { Name = "Padang", Latitude = -0.9471, Longitude = 100.4172, TimeZoneOffset = 7.0 },
            new CityLocation { Name = "Pekanbaru", Latitude = 0.5071, Longitude = 101.4478, TimeZoneOffset = 7.0 },
            new CityLocation { Name = "Banda Aceh", Latitude = 5.5483, Longitude = 95.3238, TimeZoneOffset = 7.0 },
            new CityLocation { Name = "Denpasar", Latitude = -8.6705, Longitude = 115.2126, TimeZoneOffset = 8.0 },
            new CityLocation { Name = "Makassar", Latitude = -5.1477, Longitude = 119.4327, TimeZoneOffset = 8.0 },
            new CityLocation { Name = "Banjarmasin", Latitude = -3.3194, Longitude = 114.5908, TimeZoneOffset = 8.0 },
            new CityLocation { Name = "Balikpapan", Latitude = -1.2379, Longitude = 116.8529, TimeZoneOffset = 8.0 },
            new CityLocation { Name = "Samarinda", Latitude = -0.5022, Longitude = 117.1536, TimeZoneOffset = 8.0 },
            new CityLocation { Name = "Pontianak", Latitude = -0.0263, Longitude = 109.3425, TimeZoneOffset = 7.0 },
            new CityLocation { Name = "Manado", Latitude = 1.4748, Longitude = 124.8421, TimeZoneOffset = 8.0 },
            new CityLocation { Name = "Mataram", Latitude = -8.5833, Longitude = 116.1167, TimeZoneOffset = 8.0 },
            new CityLocation { Name = "Ambon", Latitude = -3.6954, Longitude = 128.1814, TimeZoneOffset = 9.0 },
            new CityLocation { Name = "Jayapura", Latitude = -2.5489, Longitude = 140.7181, TimeZoneOffset = 9.0 }
        };

        private PrayerSchedule? _currentSchedule;
        private Timer? _tickerTimer;
        private string _currentCityName = "Jakarta";
        private double _latitude = -6.2088;
        private double _longitude = 106.8456;
        private double _timeZoneOffset = 7.0;
        private string? _lastAlertedPrayer;

        public PrayerSchedule? CurrentSchedule => _currentSchedule;
        public IReadOnlyList<CityLocation> AvailableCities => PresetCities;
        public event EventHandler<PrayerSchedule>? ScheduleUpdated;
        public event EventHandler<string>? PrayerTimeReached;

        public PrayerTimesService()
        {
            UpdateLocation("Jakarta", -6.2088, 106.8456);
        }

        public void UpdateLocation(string cityName, double latitude, double longitude)
        {
            _currentCityName = cityName;
            _latitude = latitude;
            _longitude = longitude;

            var match = PresetCities.FirstOrDefault(c => c.Name.Equals(cityName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                _timeZoneOffset = match.TimeZoneOffset;
            }
            else
            {
                // Auto-detect Indonesian time zone by longitude if inside Indonesia
                if (longitude >= 95.0 && longitude < 110.0)
                {
                    _timeZoneOffset = 7.0; // WIB
                }
                else if (longitude >= 110.0 && longitude < 125.0)
                {
                    _timeZoneOffset = latitude < -1.0 && longitude < 114.5 ? 7.0 : 8.0; // Java is WIB (7.0), Bali/KalSel/Sulawesi WITA (8.0)
                    if (longitude < 115.0 && latitude < -5.5) _timeZoneOffset = 7.0; // East Java is WIB
                }
                else if (longitude >= 125.0 && longitude <= 142.0)
                {
                    _timeZoneOffset = 9.0; // WIT
                }
                else
                {
                    _timeZoneOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalHours;
                }
            }

            Recalculate();
        }

        public void StartMonitoring()
        {
            _tickerTimer?.Dispose();
            // Ticker every 10 seconds to update remaining time countdown and detect prayer arrival
            _tickerTimer = new Timer(_ => Recalculate(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        }

        public void StopMonitoring()
        {
            _tickerTimer?.Dispose();
            _tickerTimer = null;
        }

        private void Recalculate()
        {
            try
            {
                var schedule = CalculateToday(_latitude, _longitude, _timeZoneOffset);
                _currentSchedule = schedule;
                ScheduleUpdated?.Invoke(this, schedule);

                // Check if remaining to next prayer is <= 15 seconds (time arrived)
                if (schedule.RemainingToNext.TotalSeconds <= 15 && schedule.RemainingToNext.TotalSeconds >= 0)
                {
                    string prayerKey = $"{DateTime.Today:yyyyMMdd}-{schedule.NextPrayerName}";
                    if (_lastAlertedPrayer != prayerKey)
                    {
                        _lastAlertedPrayer = prayerKey;
                        PrayerTimeReached?.Invoke(this, $"Waktu sholat {schedule.NextPrayerName} telah tiba ({schedule.NextPrayerTime:hh\\:mm})");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to recalculate prayer times");
            }
        }

        public PrayerSchedule CalculateToday(double lat, double lon, double tz)
        {
            var now = DateTime.Now;
            int year = now.Year;
            int month = now.Month;
            int day = now.Day;

            // Astronomical Julian Day
            if (month <= 2) { year -= 1; month += 12; }
            double a = Math.Floor(year / 100.0);
            double b = 2 - a + Math.Floor(a / 4.0);
            double jd = Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;

            // Sun coordinates
            double d = jd - 2451545.0;
            double g = FixAngle(357.529 + 0.98560028 * d);
            double q = FixAngle(280.459 + 0.98564736 * d);
            double l = FixAngle(q + 1.915 * Sin(g) + 0.020 * Sin(2 * g));
            double e = 23.439 - 0.00000036 * d;
            double declination = ArcSin(Sin(e) * Sin(l));
            double ra = ArcTan2(Cos(e) * Sin(l), Cos(l)) / 15.0;
            ra = FixHour(ra);
            double eqT = q / 15.0 - ra;
            if (eqT > 12.0) eqT -= 24.0;
            if (eqT < -12.0) eqT += 24.0;

            // Solar Noon (transit in local hours)
            double noon = 12.0 + tz - (lon / 15.0) - eqT;

            // Subuh (Fajr): Sun altitude -20.0 deg (Kemenag RI standard)
            double fajrHa = HourAngle(-20.0, lat, declination);
            double fSubuh = noon - fajrHa;

            // Sunrise (Terbit): Sun altitude -0.8333 deg
            double sunriseHa = HourAngle(-0.8333, lat, declination);
            double fTerbit = noon - sunriseHa;

            // Sunset (Maghrib): Sun altitude -0.8333 deg
            double fMaghrib = noon + sunriseHa;

            // Ashar: Shafi'i method (shadow length = 1.0)
            // altitude = acot(1 + tan(|lat - decl|))
            double asrAltitude = RadToDeg(Math.Atan(1.0 / (1.0 + Math.Tan(DegToRad(Math.Abs(lat - declination))))));
            double asrHa = HourAngle(asrAltitude, lat, declination);
            double fAshar = noon + asrHa;

            // Isya (Isha): Sun altitude -18.0 deg (Kemenag RI standard)
            double ishaHa = HourAngle(-18.0, lat, declination);
            double fIsya = noon + ishaHa;

            // Standard Kemenag RI Ihtiyat (safety buffer) = +2 minutes (2/60 hour)
            double ihtiyat = 2.0 / 60.0;
            fSubuh += ihtiyat;
            double fDzuhur = noon + ihtiyat;
            fAshar += ihtiyat;
            fMaghrib += ihtiyat;
            fIsya += ihtiyat;

            var tsSubuh = DoubleToTimeSpan(fSubuh);
            var tsTerbit = DoubleToTimeSpan(fTerbit);

            // Dhuha: Sun altitude +4.5 deg (Kemenag RI standard, ~20 mins after sunrise)
            double dhuhaHa = HourAngle(4.5, lat, declination);
            double fDhuha = double.IsNaN(dhuhaHa) ? (fTerbit + 20.0 / 60.0) : (noon - dhuhaHa + ihtiyat);
            var tsDhuha = DoubleToTimeSpan(fDhuha);

            var tsDzuhur = DoubleToTimeSpan(fDzuhur);
            var tsAshar = DoubleToTimeSpan(fAshar);
            var tsMaghrib = DoubleToTimeSpan(fMaghrib);
            var tsIsya = DoubleToTimeSpan(fIsya);
            var tsImsak = tsSubuh.Subtract(TimeSpan.FromMinutes(10));

            // Determine Next Prayer & Countdown
            var currentTime = now.TimeOfDay;
            string nextName;
            TimeSpan nextTime;
            TimeSpan rem;
            int activeIdx;

            if (currentTime < tsSubuh)
            {
                nextName = "Subuh";
                nextTime = tsSubuh;
                rem = tsSubuh - currentTime;
                activeIdx = 0; // Waktu akan datang: Subuh
            }
            else if (currentTime < tsDhuha)
            {
                nextName = "Dhuha";
                nextTime = tsDhuha;
                rem = tsDhuha - currentTime;
                activeIdx = 1; // Waktu akan datang: Dhuha
            }
            else if (currentTime < tsDzuhur)
            {
                nextName = "Dzuhur";
                nextTime = tsDzuhur;
                rem = tsDzuhur - currentTime;
                activeIdx = 2; // Waktu akan datang: Dzuhur
            }
            else if (currentTime < tsAshar)
            {
                nextName = "Ashar";
                nextTime = tsAshar;
                rem = tsAshar - currentTime;
                activeIdx = 3; // Waktu akan datang: Ashar
            }
            else if (currentTime < tsMaghrib)
            {
                nextName = "Maghrib";
                nextTime = tsMaghrib;
                rem = tsMaghrib - currentTime;
                activeIdx = 4; // Waktu akan datang: Maghrib
            }
            else if (currentTime < tsIsya)
            {
                nextName = "Isya";
                nextTime = tsIsya;
                rem = tsIsya - currentTime;
                activeIdx = 5; // Waktu akan datang: Isya
            }
            else
            {
                // After Isha -> Subuh tomorrow
                nextName = "Subuh (Besok)";
                nextTime = tsSubuh;
                rem = TimeSpan.FromHours(24) - currentTime + tsSubuh;
                activeIdx = 0; // Waktu akan datang: Subuh besok
            }

            return new PrayerSchedule
            {
                Date = DateTime.Today,
                Imsak = tsImsak,
                Subuh = tsSubuh,
                Terbit = tsTerbit,
                Dhuha = tsDhuha,
                Dzuhur = tsDzuhur,
                Ashar = tsAshar,
                Maghrib = tsMaghrib,
                Isya = tsIsya,
                NextPrayerName = nextName,
                NextPrayerTime = nextTime,
                RemainingToNext = rem,
                ActivePrayerIndex = activeIdx
            };
        }

        private static double HourAngle(double altitude, double lat, double declination)
        {
            double rLat = DegToRad(lat);
            double rDec = DegToRad(declination);
            double top = Math.Sin(DegToRad(altitude)) - Math.Sin(rLat) * Math.Sin(rDec);
            double bot = Math.Cos(rLat) * Math.Cos(rDec);
            double val = Math.Clamp(top / bot, -1.0, 1.0);
            return (1.0 / 15.0) * RadToDeg(Math.Acos(val));
        }

        private static TimeSpan DoubleToTimeSpan(double hours)
        {
            hours = FixHour(hours);
            int h = (int)Math.Floor(hours);
            double remM = (hours - h) * 60.0;
            int m = (int)Math.Floor(remM + 0.5); // Round to nearest minute
            if (m >= 60)
            {
                m = 0;
                h = (h + 1) % 24;
            }
            return new TimeSpan(h, m, 0);
        }

        private static double DegToRad(double d) => d * Math.PI / 180.0;
        private static double RadToDeg(double r) => r * 180.0 / Math.PI;
        private static double Sin(double d) => Math.Sin(DegToRad(d));
        private static double Cos(double d) => Math.Cos(DegToRad(d));
        private static double ArcSin(double x) => RadToDeg(Math.Asin(Math.Clamp(x, -1.0, 1.0)));
        private static double ArcTan2(double y, double x) => RadToDeg(Math.Atan2(y, x));
        private static double FixAngle(double a) { a %= 360.0; return a < 0 ? a + 360.0 : a; }
        private static double FixHour(double h) { h %= 24.0; return h < 0 ? h + 24.0 : h; }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}
