using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Clock
{
    public sealed class ClockService
    {
        private static readonly Lazy<ClockService> _instance = new(() => new ClockService());
        public static ClockService Instance => _instance.Value;

        private const string PrefsFileName = "clock_prefs.json";
        private readonly DispatcherTimer _tickTimer;
        private readonly object _lock = new();

        public event Action SecondTick;
        public event Action SettingsChanged;

        public ClockSettingsModel Settings { get; private set; } = new();

        public static readonly List<CuratedCityEntry> CuratedCities = new()
        {
            new CuratedCityEntry { CityName = "New York", TimeZoneId = "Eastern Standard Time", CountryOrRegion = "United States" },
            new CuratedCityEntry { CityName = "London", TimeZoneId = "GMT Standard Time", CountryOrRegion = "United Kingdom" },
            new CuratedCityEntry { CityName = "Tokyo", TimeZoneId = "Tokyo Standard Time", CountryOrRegion = "Japan" },
            new CuratedCityEntry { CityName = "Paris", TimeZoneId = "Romance Standard Time", CountryOrRegion = "France" },
            new CuratedCityEntry { CityName = "Dubai", TimeZoneId = "Arabian Standard Time", CountryOrRegion = "United Arab Emirates" },
            new CuratedCityEntry { CityName = "Sydney", TimeZoneId = "AUS Eastern Standard Time", CountryOrRegion = "Australia" },
            new CuratedCityEntry { CityName = "Berlin", TimeZoneId = "W. Europe Standard Time", CountryOrRegion = "Germany" },
            new CuratedCityEntry { CityName = "Los Angeles", TimeZoneId = "Pacific Standard Time", CountryOrRegion = "United States" },
            new CuratedCityEntry { CityName = "Singapore", TimeZoneId = "Singapore Standard Time", CountryOrRegion = "Singapore" },
            new CuratedCityEntry { CityName = "Toronto", TimeZoneId = "Eastern Standard Time", CountryOrRegion = "Canada" },
            new CuratedCityEntry { CityName = "Hong Kong", TimeZoneId = "China Standard Time", CountryOrRegion = "China" },
            new CuratedCityEntry { CityName = "Seoul", TimeZoneId = "Korea Standard Time", CountryOrRegion = "South Korea" },
            new CuratedCityEntry { CityName = "Istanbul", TimeZoneId = "Turkey Standard Time", CountryOrRegion = "Turkey" },
            new CuratedCityEntry { CityName = "Mumbai", TimeZoneId = "India Standard Time", CountryOrRegion = "India" }
        };

        private ClockService()
        {
            Load();

            _tickTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _tickTimer.Tick += (_, _) => SecondTick?.Invoke();
            _tickTimer.Start();
        }

        public void Load()
        {
            lock (_lock)
            {
                try
                {
                    var saved = PersistenceHelper.SafeReadJson<ClockSettingsModel>(PrefsFileName);
                    if (saved != null && saved.WorldClocks != null && saved.WorldClocks.Count > 0)
                    {
                        Settings = saved;
                    }
                    else
                    {
                        InitDefaults();
                    }
                }
                catch
                {
                    InitDefaults();
                }
            }
        }

        private void InitDefaults()
        {
            Settings = new ClockSettingsModel
            {
                Format = ClockFormat.TwelveHour,
                DisplayMode = ClockDisplayMode.Digital,
                ShowSeconds = false,
                ShowDate = true,
                DateFormat = ClockDateFormat.Full,
                WorldClocks = new List<WorldClockCity>
                {
                    new WorldClockCity { CityName = "Local", TimeZoneId = TimeZoneInfo.Local.Id, IsLocal = true },
                    new WorldClockCity { CityName = "New York", TimeZoneId = "Eastern Standard Time" },
                    new WorldClockCity { CityName = "London", TimeZoneId = "GMT Standard Time" },
                    new WorldClockCity { CityName = "Tokyo", TimeZoneId = "Tokyo Standard Time" }
                }
            };
            Save();
        }

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    PersistenceHelper.SafeWriteJson(PrefsFileName, Settings);
                }
                catch { }
            }
        }

        public void NotifySettingsChanged()
        {
            SettingsChanged?.Invoke();
        }

        public void ToggleFormat()
        {
            Settings.Format = Settings.Format == ClockFormat.TwelveHour ? ClockFormat.TwentyFourHour : ClockFormat.TwelveHour;
            Save();
            SettingsChanged?.Invoke();
        }

        public void SetDisplayMode(ClockDisplayMode mode)
        {
            if (Settings.DisplayMode != mode)
            {
                Settings.DisplayMode = mode;
                Save();
                SettingsChanged?.Invoke();
            }
        }

        public void ToggleSeconds()
        {
            Settings.ShowSeconds = !Settings.ShowSeconds;
            Save();
            SettingsChanged?.Invoke();
        }

        public void ToggleDate()
        {
            Settings.ShowDate = !Settings.ShowDate;
            Save();
            SettingsChanged?.Invoke();
        }

        public void SetDateFormat(ClockDateFormat fmt)
        {
            if (Settings.DateFormat != fmt)
            {
                Settings.DateFormat = fmt;
                Save();
                SettingsChanged?.Invoke();
            }
        }

        public void AddWorldClock(CuratedCityEntry city)
        {
            if (city == null) return;
            lock (_lock)
            {
                if (Settings.WorldClocks.Any(c => string.Equals(c.CityName, city.CityName, StringComparison.OrdinalIgnoreCase)))
                    return;

                Settings.WorldClocks.Add(new WorldClockCity
                {
                    CityName = city.CityName,
                    TimeZoneId = city.TimeZoneId,
                    IsLocal = false
                });
                Save();
                SettingsChanged?.Invoke();
            }
        }

        public void RemoveWorldClock(string id)
        {
            lock (_lock)
            {
                var target = Settings.WorldClocks.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
                if (target != null && !target.IsLocal)
                {
                    Settings.WorldClocks.Remove(target);
                    Save();
                    SettingsChanged?.Invoke();
                }
            }
        }

        public string FormatTime(DateTime dt, bool forceSeconds = false)
        {
            bool showSec = forceSeconds || Settings.ShowSeconds;
            bool is24 = Settings.Format == ClockFormat.TwentyFourHour;
            if (Settings.Format == ClockFormat.System)
            {
                is24 = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("H");
            }

            if (is24)
            {
                return showSec ? dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : dt.ToString("HH:mm", CultureInfo.InvariantCulture);
            }
            return showSec ? dt.ToString("h:mm:ss tt", CultureInfo.InvariantCulture) : dt.ToString("h:mm tt", CultureInfo.InvariantCulture);
        }

        public string FormatDate(DateTime dt)
        {
            return Settings.DateFormat switch
            {
                ClockDateFormat.Compact => dt.ToString("M/d/yyyy", CultureInfo.InvariantCulture),
                ClockDateFormat.Short => dt.ToString("ddd, MMM d, yyyy", CultureInfo.InvariantCulture),
                _ => dt.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture)
            };
        }

        public TimeZoneInfo ResolveTimeZone(string tzId)
        {
            if (string.IsNullOrWhiteSpace(tzId)) return TimeZoneInfo.Local;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }

        public (DateTime time, string subtext) GetCityInfo(WorldClockCity city)
        {
            DateTime utcNow = DateTime.UtcNow;
            DateTime localNow = DateTime.Now;
            TimeZoneInfo localTz = TimeZoneInfo.Local;

            if (city.IsLocal)
            {
                TimeSpan offset = localTz.GetUtcOffset(utcNow);
                string sign = offset >= TimeSpan.Zero ? "+" : "-";
                string offStr = $"UTC{sign}{Math.Abs(offset.Hours)}:{Math.Abs(offset.Minutes):D2}";
                return (localNow, offStr);
            }

            TimeZoneInfo tz = ResolveTimeZone(city.TimeZoneId);
            DateTime cityTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);

            TimeSpan cityOffset = tz.GetUtcOffset(utcNow);
            TimeSpan localOffset = localTz.GetUtcOffset(utcNow);
            TimeSpan diff = cityOffset - localOffset;

            string abbrev = GetTimeZoneAbbrev(tz, utcNow);
            string diffStr = FormatDifference(diff, cityTime.Date, localNow.Date);

            return (cityTime, $"{abbrev} · {diffStr}");
        }

        private static string FormatDifference(TimeSpan diff, DateTime cityDate, DateTime localDate)
        {
            string sign = diff >= TimeSpan.Zero ? "+" : "-";
            int hours = Math.Abs((int)diff.TotalHours);
            int mins = Math.Abs(diff.Minutes);

            string s = $"{sign}{hours}h";
            if (mins > 0) s += $" {mins}m";

            if (cityDate > localDate) s += ", tomorrow";
            else if (cityDate < localDate) s += ", yesterday";

            return s;
        }

        private static string GetTimeZoneAbbrev(TimeZoneInfo tz, DateTime utc)
        {
            bool isDst = tz.IsDaylightSavingTime(TimeZoneInfo.ConvertTimeFromUtc(utc, tz));
            string id = tz.Id;

            if (id.Contains("Eastern")) return isDst ? "EDT" : "EST";
            if (id.Contains("Pacific")) return isDst ? "PDT" : "PST";
            if (id.Contains("Central")) return isDst ? "CDT" : "CST";
            if (id.Contains("Mountain")) return isDst ? "MDT" : "MST";
            if (id.Contains("GMT") || id.Contains("UTC")) return "GMT";
            if (id.Contains("Tokyo")) return "JST";
            if (id.Contains("Romance") || id.Contains("W. Europe")) return isDst ? "CEST" : "CET";
            if (id.Contains("Arabian")) return "GST";
            if (id.Contains("AUS Eastern")) return isDst ? "AEDT" : "AEST";
            if (id.Contains("Singapore")) return "SGT";
            if (id.Contains("China")) return "CST";
            if (id.Contains("Korea")) return "KST";
            if (id.Contains("India")) return "IST";
            if (id.Contains("Iran")) return "IRST";

            return tz.StandardName.Length > 4 ? tz.StandardName.Substring(0, 3).ToUpperInvariant() : tz.StandardName;
        }
    }
}
