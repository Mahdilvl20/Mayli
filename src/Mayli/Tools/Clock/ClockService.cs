using System;
using System.Collections.Concurrent;
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

        private int _lastChimeHour = -1;

        #region Curated Global Cities Database (80+ Major Cities)
        public static readonly List<CuratedCityEntry> CuratedCities = new()
        {
            // Iran & Middle East
            new CuratedCityEntry { CityName = "Tehran", CountryOrRegion = "Iran", TimeZoneId = "Iran Standard Time", FlagOrIcon = "🇮🇷", Latitude = 35.6892, Longitude = 51.3890 },
            new CuratedCityEntry { CityName = "Mashhad", CountryOrRegion = "Iran", TimeZoneId = "Iran Standard Time", FlagOrIcon = "🇮🇷", Latitude = 36.2972, Longitude = 59.6067 },
            new CuratedCityEntry { CityName = "Isfahan", CountryOrRegion = "Iran", TimeZoneId = "Iran Standard Time", FlagOrIcon = "🇮🇷", Latitude = 32.6546, Longitude = 51.6680 },
            new CuratedCityEntry { CityName = "Shiraz", CountryOrRegion = "Iran", TimeZoneId = "Iran Standard Time", FlagOrIcon = "🇮🇷", Latitude = 29.5918, Longitude = 52.5837 },
            new CuratedCityEntry { CityName = "Tabriz", CountryOrRegion = "Iran", TimeZoneId = "Iran Standard Time", FlagOrIcon = "🇮🇷", Latitude = 38.0800, Longitude = 46.2919 },
            new CuratedCityEntry { CityName = "Dubai", CountryOrRegion = "United Arab Emirates", TimeZoneId = "Arabian Standard Time", FlagOrIcon = "🇦🇪", Latitude = 25.2048, Longitude = 55.2708 },
            new CuratedCityEntry { CityName = "Abu Dhabi", CountryOrRegion = "United Arab Emirates", TimeZoneId = "Arabian Standard Time", FlagOrIcon = "🇦🇪", Latitude = 24.4539, Longitude = 54.3773 },
            new CuratedCityEntry { CityName = "Doha", CountryOrRegion = "Qatar", TimeZoneId = "Arab Standard Time", FlagOrIcon = "🇶🇦", Latitude = 25.2854, Longitude = 51.5310 },
            new CuratedCityEntry { CityName = "Riyadh", CountryOrRegion = "Saudi Arabia", TimeZoneId = "Arab Standard Time", FlagOrIcon = "🇸🇦", Latitude = 24.7136, Longitude = 46.6753 },
            new CuratedCityEntry { CityName = "Kuwait City", CountryOrRegion = "Kuwait", TimeZoneId = "Arab Standard Time", FlagOrIcon = "🇰🇼", Latitude = 29.3759, Longitude = 47.9774 },
            new CuratedCityEntry { CityName = "Muscat", CountryOrRegion = "Oman", TimeZoneId = "Arabian Standard Time", FlagOrIcon = "🇴🇲", Latitude = 23.5880, Longitude = 58.3829 },
            new CuratedCityEntry { CityName = "Baghdad", CountryOrRegion = "Iraq", TimeZoneId = "Arabic Standard Time", FlagOrIcon = "🇮🇶", Latitude = 33.3152, Longitude = 44.3661 },
            new CuratedCityEntry { CityName = "Istanbul", CountryOrRegion = "Turkey", TimeZoneId = "Turkey Standard Time", FlagOrIcon = "🇹🇷", Latitude = 41.0082, Longitude = 28.9784 },
            new CuratedCityEntry { CityName = "Ankara", CountryOrRegion = "Turkey", TimeZoneId = "Turkey Standard Time", FlagOrIcon = "🇹🇷", Latitude = 39.9334, Longitude = 32.8597 },
            new CuratedCityEntry { CityName = "Baku", CountryOrRegion = "Azerbaijan", TimeZoneId = "Azerbaijan Standard Time", FlagOrIcon = "🇦🇿", Latitude = 40.4093, Longitude = 49.8671 },
            new CuratedCityEntry { CityName = "Yerevan", CountryOrRegion = "Armenia", TimeZoneId = "Caucasus Standard Time", FlagOrIcon = "🇦🇲", Latitude = 40.1792, Longitude = 44.4991 },

            // Europe
            new CuratedCityEntry { CityName = "London", CountryOrRegion = "United Kingdom", TimeZoneId = "GMT Standard Time", FlagOrIcon = "🇬🇧", Latitude = 51.5074, Longitude = -0.1278 },
            new CuratedCityEntry { CityName = "Paris", CountryOrRegion = "France", TimeZoneId = "Romance Standard Time", FlagOrIcon = "🇫🇷", Latitude = 48.8566, Longitude = 2.3522 },
            new CuratedCityEntry { CityName = "Berlin", CountryOrRegion = "Germany", TimeZoneId = "W. Europe Standard Time", FlagOrIcon = "🇩🇪", Latitude = 52.5200, Longitude = 13.4050 },
            new CuratedCityEntry { CityName = "Frankfurt", CountryOrRegion = "Germany", TimeZoneId = "W. Europe Standard Time", FlagOrIcon = "🇩🇪", Latitude = 50.1109, Longitude = 8.6821 },
            new CuratedCityEntry { CityName = "Rome", CountryOrRegion = "Italy", TimeZoneId = "W. Europe Standard Time", FlagOrIcon = "🇮🇹", Latitude = 41.9028, Longitude = 12.4964 },
            new CuratedCityEntry { CityName = "Madrid", CountryOrRegion = "Spain", TimeZoneId = "Romance Standard Time", FlagOrIcon = "🇪🇸", Latitude = 40.4168, Longitude = -3.7038 },
            new CuratedCityEntry { CityName = "Barcelona", CountryOrRegion = "Spain", TimeZoneId = "Romance Standard Time", FlagOrIcon = "🇪🇸", Latitude = 41.3851, Longitude = 2.1734 },
            new CuratedCityEntry { CityName = "Amsterdam", CountryOrRegion = "Netherlands", TimeZoneId = "W. Europe Standard Time", FlagOrIcon = "🇳🇱", Latitude = 52.3676, Longitude = 4.9041 },
            new CuratedCityEntry { CityName = "Brussels", CountryOrRegion = "Belgium", TimeZoneId = "Romance Standard Time", FlagOrIcon = "🇧🇪", Latitude = 50.8503, Longitude = 4.3517 },
            new CuratedCityEntry { CityName = "Zurich", CountryOrRegion = "Switzerland", TimeZoneId = "W. Europe Standard Time", FlagOrIcon = "🇨🇭", Latitude = 47.3769, Longitude = 8.5417 },
            new CuratedCityEntry { CityName = "Vienna", CountryOrRegion = "Austria", TimeZoneId = "W. Europe Standard Time", FlagOrIcon = "🇦🇹", Latitude = 48.2082, Longitude = 16.3738 },
            new CuratedCityEntry { CityName = "Stockholm", CountryOrRegion = "Sweden", TimeZoneId = "W. Europe Standard Time", FlagOrIcon = "🇸🇪", Latitude = 59.3293, Longitude = 18.0686 },
            new CuratedCityEntry { CityName = "Oslo", CountryOrRegion = "Norway", TimeZoneId = "W. Europe Standard Time", FlagOrIcon = "🇳🇴", Latitude = 59.9139, Longitude = 10.7522 },
            new CuratedCityEntry { CityName = "Copenhagen", CountryOrRegion = "Denmark", TimeZoneId = "Romance Standard Time", FlagOrIcon = "🇩🇰", Latitude = 55.6761, Longitude = 12.5683 },
            new CuratedCityEntry { CityName = "Helsinki", CountryOrRegion = "Finland", TimeZoneId = "FLE Standard Time", FlagOrIcon = "🇫🇮", Latitude = 60.1699, Longitude = 24.9384 },
            new CuratedCityEntry { CityName = "Dublin", CountryOrRegion = "Ireland", TimeZoneId = "GMT Standard Time", FlagOrIcon = "🇮🇪", Latitude = 53.3498, Longitude = -6.2603 },
            new CuratedCityEntry { CityName = "Lisbon", CountryOrRegion = "Portugal", TimeZoneId = "GMT Standard Time", FlagOrIcon = "🇵🇹", Latitude = 38.7223, Longitude = -9.1393 },
            new CuratedCityEntry { CityName = "Athens", CountryOrRegion = "Greece", TimeZoneId = "GTB Standard Time", FlagOrIcon = "🇬🇷", Latitude = 37.9838, Longitude = 23.7275 },
            new CuratedCityEntry { CityName = "Warsaw", CountryOrRegion = "Poland", TimeZoneId = "Central European Standard Time", FlagOrIcon = "🇵🇱", Latitude = 52.2297, Longitude = 21.0122 },
            new CuratedCityEntry { CityName = "Prague", CountryOrRegion = "Czech Republic", TimeZoneId = "Central Europe Standard Time", FlagOrIcon = "🇨🇿", Latitude = 50.0755, Longitude = 14.4378 },
            new CuratedCityEntry { CityName = "Kyiv", CountryOrRegion = "Ukraine", TimeZoneId = "FLE Standard Time", FlagOrIcon = "🇺🇦", Latitude = 50.4501, Longitude = 30.5234 },
            new CuratedCityEntry { CityName = "Moscow", CountryOrRegion = "Russia", TimeZoneId = "Russian Standard Time", FlagOrIcon = "🇷🇺", Latitude = 55.7558, Longitude = 37.6173 },

            // North America
            new CuratedCityEntry { CityName = "New York", CountryOrRegion = "United States", TimeZoneId = "Eastern Standard Time", FlagOrIcon = "🇺🇸", Latitude = 40.7128, Longitude = -74.0060 },
            new CuratedCityEntry { CityName = "Washington D.C.", CountryOrRegion = "United States", TimeZoneId = "Eastern Standard Time", FlagOrIcon = "🇺🇸", Latitude = 38.9072, Longitude = -77.0369 },
            new CuratedCityEntry { CityName = "Los Angeles", CountryOrRegion = "United States", TimeZoneId = "Pacific Standard Time", FlagOrIcon = "🇺🇸", Latitude = 34.0522, Longitude = -118.2437 },
            new CuratedCityEntry { CityName = "San Francisco", CountryOrRegion = "United States", TimeZoneId = "Pacific Standard Time", FlagOrIcon = "🇺🇸", Latitude = 37.7749, Longitude = -122.4194 },
            new CuratedCityEntry { CityName = "Chicago", CountryOrRegion = "United States", TimeZoneId = "Central Standard Time", FlagOrIcon = "🇺🇸", Latitude = 41.8781, Longitude = -87.6298 },
            new CuratedCityEntry { CityName = "Miami", CountryOrRegion = "United States", TimeZoneId = "Eastern Standard Time", FlagOrIcon = "🇺🇸", Latitude = 25.7617, Longitude = -80.1918 },
            new CuratedCityEntry { CityName = "Seattle", CountryOrRegion = "United States", TimeZoneId = "Pacific Standard Time", FlagOrIcon = "🇺🇸", Latitude = 47.6062, Longitude = -122.3321 },
            new CuratedCityEntry { CityName = "Boston", CountryOrRegion = "United States", TimeZoneId = "Eastern Standard Time", FlagOrIcon = "🇺🇸", Latitude = 42.3601, Longitude = -71.0589 },
            new CuratedCityEntry { CityName = "Denver", CountryOrRegion = "United States", TimeZoneId = "Mountain Standard Time", FlagOrIcon = "🇺🇸", Latitude = 39.7392, Longitude = -104.9903 },
            new CuratedCityEntry { CityName = "Dallas", CountryOrRegion = "United States", TimeZoneId = "Central Standard Time", FlagOrIcon = "🇺🇸", Latitude = 32.7767, Longitude = -96.7970 },
            new CuratedCityEntry { CityName = "Toronto", CountryOrRegion = "Canada", TimeZoneId = "Eastern Standard Time", FlagOrIcon = "🇨🇦", Latitude = 43.6532, Longitude = -79.3832 },
            new CuratedCityEntry { CityName = "Vancouver", CountryOrRegion = "Canada", TimeZoneId = "Pacific Standard Time", FlagOrIcon = "🇨🇦", Latitude = 49.2827, Longitude = -123.1207 },
            new CuratedCityEntry { CityName = "Montreal", CountryOrRegion = "Canada", TimeZoneId = "Eastern Standard Time", FlagOrIcon = "🇨🇦", Latitude = 45.5017, Longitude = -73.5673 },
            new CuratedCityEntry { CityName = "Mexico City", CountryOrRegion = "Mexico", TimeZoneId = "Central Standard Time (Mexico)", FlagOrIcon = "🇲🇽", Latitude = 19.4326, Longitude = -99.1332 },

            // South America
            new CuratedCityEntry { CityName = "São Paulo", CountryOrRegion = "Brazil", TimeZoneId = "E. South America Standard Time", FlagOrIcon = "🇧🇷", Latitude = -23.5505, Longitude = -46.6333 },
            new CuratedCityEntry { CityName = "Rio de Janeiro", CountryOrRegion = "Brazil", TimeZoneId = "E. South America Standard Time", FlagOrIcon = "🇧🇷", Latitude = -22.9068, Longitude = -43.1729 },
            new CuratedCityEntry { CityName = "Buenos Aires", CountryOrRegion = "Argentina", TimeZoneId = "Argentina Standard Time", FlagOrIcon = "🇦🇷", Latitude = -34.6037, Longitude = -58.3816 },
            new CuratedCityEntry { CityName = "Santiago", CountryOrRegion = "Chile", TimeZoneId = "Pacific SA Standard Time", FlagOrIcon = "🇨🇱", Latitude = -33.4489, Longitude = -70.6693 },
            new CuratedCityEntry { CityName = "Bogota", CountryOrRegion = "Colombia", TimeZoneId = "SA Pacific Standard Time", FlagOrIcon = "🇨🇴", Latitude = 4.7110, Longitude = -74.0721 },

            // Asia & Pacific
            new CuratedCityEntry { CityName = "Tokyo", CountryOrRegion = "Japan", TimeZoneId = "Tokyo Standard Time", FlagOrIcon = "🇯🇵", Latitude = 35.6762, Longitude = 139.6503 },
            new CuratedCityEntry { CityName = "Osaka", CountryOrRegion = "Japan", TimeZoneId = "Tokyo Standard Time", FlagOrIcon = "🇯🇵", Latitude = 34.6937, Longitude = 135.5023 },
            new CuratedCityEntry { CityName = "Seoul", CountryOrRegion = "South Korea", TimeZoneId = "Korea Standard Time", FlagOrIcon = "🇰🇷", Latitude = 37.5665, Longitude = 126.9780 },
            new CuratedCityEntry { CityName = "Beijing", CountryOrRegion = "China", TimeZoneId = "China Standard Time", FlagOrIcon = "🇨🇳", Latitude = 39.9042, Longitude = 116.4074 },
            new CuratedCityEntry { CityName = "Shanghai", CountryOrRegion = "China", TimeZoneId = "China Standard Time", FlagOrIcon = "🇨🇳", Latitude = 31.2304, Longitude = 121.4737 },
            new CuratedCityEntry { CityName = "Hong Kong", CountryOrRegion = "Hong Kong", TimeZoneId = "China Standard Time", FlagOrIcon = "🇭🇰", Latitude = 22.3193, Longitude = 114.1694 },
            new CuratedCityEntry { CityName = "Taipei", CountryOrRegion = "Taiwan", TimeZoneId = "Taipei Standard Time", FlagOrIcon = "🇹🇼", Latitude = 25.0330, Longitude = 121.5654 },
            new CuratedCityEntry { CityName = "Singapore", CountryOrRegion = "Singapore", TimeZoneId = "Singapore Standard Time", FlagOrIcon = "🇸🇬", Latitude = 1.3521, Longitude = 103.8198 },
            new CuratedCityEntry { CityName = "Bangkok", CountryOrRegion = "Thailand", TimeZoneId = "SE Asia Standard Time", FlagOrIcon = "🇹🇭", Latitude = 13.7563, Longitude = 100.5018 },
            new CuratedCityEntry { CityName = "Kuala Lumpur", CountryOrRegion = "Malaysia", TimeZoneId = "Singapore Standard Time", FlagOrIcon = "🇲🇾", Latitude = 3.1390, Longitude = 101.6869 },
            new CuratedCityEntry { CityName = "Jakarta", CountryOrRegion = "Indonesia", TimeZoneId = "SE Asia Standard Time", FlagOrIcon = "🇮🇩", Latitude = -6.2088, Longitude = 106.8456 },
            new CuratedCityEntry { CityName = "Manila", CountryOrRegion = "Philippines", TimeZoneId = "Singapore Standard Time", FlagOrIcon = "🇵🇭", Latitude = 14.5995, Longitude = 120.9842 },
            new CuratedCityEntry { CityName = "Mumbai", CountryOrRegion = "India", TimeZoneId = "India Standard Time", FlagOrIcon = "🇮🇳", Latitude = 19.0760, Longitude = 72.8777 },
            new CuratedCityEntry { CityName = "New Delhi", CountryOrRegion = "India", TimeZoneId = "India Standard Time", FlagOrIcon = "🇮🇳", Latitude = 28.6139, Longitude = 77.2090 },
            new CuratedCityEntry { CityName = "Karachi", CountryOrRegion = "Pakistan", TimeZoneId = "Pakistan Standard Time", FlagOrIcon = "🇵🇰", Latitude = 24.8607, Longitude = 67.0011 },
            new CuratedCityEntry { CityName = "Sydney", CountryOrRegion = "Australia", TimeZoneId = "AUS Eastern Standard Time", FlagOrIcon = "🇦🇺", Latitude = -33.8688, Longitude = 151.2093 },
            new CuratedCityEntry { CityName = "Melbourne", CountryOrRegion = "Australia", TimeZoneId = "AUS Eastern Standard Time", FlagOrIcon = "🇦🇺", Latitude = -37.8136, Longitude = 144.9631 },
            new CuratedCityEntry { CityName = "Perth", CountryOrRegion = "Australia", TimeZoneId = "W. Australia Standard Time", FlagOrIcon = "🇦🇺", Latitude = -31.9505, Longitude = 115.8605 },
            new CuratedCityEntry { CityName = "Auckland", CountryOrRegion = "New Zealand", TimeZoneId = "New Zealand Standard Time", FlagOrIcon = "🇳🇿", Latitude = -36.8485, Longitude = 174.7633 },

            // Africa
            new CuratedCityEntry { CityName = "Cairo", CountryOrRegion = "Egypt", TimeZoneId = "Egypt Standard Time", FlagOrIcon = "🇪🇬", Latitude = 30.0444, Longitude = 31.2357 },
            new CuratedCityEntry { CityName = "Johannesburg", CountryOrRegion = "South Africa", TimeZoneId = "South Africa Standard Time", FlagOrIcon = "🇿🇦", Latitude = -26.2041, Longitude = 28.0473 },
            new CuratedCityEntry { CityName = "Cape Town", CountryOrRegion = "South Africa", TimeZoneId = "South Africa Standard Time", FlagOrIcon = "🇿🇦", Latitude = -33.9249, Longitude = 18.4241 },
            new CuratedCityEntry { CityName = "Nairobi", CountryOrRegion = "Kenya", TimeZoneId = "E. Africa Standard Time", FlagOrIcon = "🇰🇪", Latitude = -1.2921, Longitude = 36.8219 },
            new CuratedCityEntry { CityName = "Casablanca", CountryOrRegion = "Morocco", TimeZoneId = "Morocco Standard Time", FlagOrIcon = "🇲🇦", Latitude = 33.5731, Longitude = -7.5898 },

            // UTC Reference
            new CuratedCityEntry { CityName = "UTC / GMT", CountryOrRegion = "Universal Time", TimeZoneId = "UTC", FlagOrIcon = "🌐", Latitude = 51.4769, Longitude = 0.0 }
        };
        #endregion

        private ClockService()
        {
            Load();

            _tickTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _tickTimer.Tick += (_, _) =>
            {
                CheckHourlyChime();
                SecondTick?.Invoke();
            };
            _tickTimer.Start();
        }

        private void CheckHourlyChime()
        {
            if (!Settings.HourlyChime) return;
            DateTime now = DateTime.Now;
            if (now.Minute == 0 && now.Second == 0 && now.Hour != _lastChimeHour)
            {
                _lastChimeHour = now.Hour;
                try
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }
                catch { }
            }
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
            var localTz = TimeZoneInfo.Local;
            string localName = "Local";
            double localLat = 35.6892;
            double localLon = 51.3890;

            if (localTz.Id.Contains("Iran", StringComparison.OrdinalIgnoreCase))
            {
                localName = "Tehran";
                localLat = 35.6892;
                localLon = 51.3890;
            }
            else if (localTz.Id.Contains("Eastern", StringComparison.OrdinalIgnoreCase))
            {
                localName = "New York";
                localLat = 40.7128;
                localLon = -74.0060;
            }
            else if (localTz.Id.Contains("GMT", StringComparison.OrdinalIgnoreCase))
            {
                localName = "London";
                localLat = 51.5074;
                localLon = -0.1278;
            }

            Settings = new ClockSettingsModel
            {
                Format = ClockFormat.TwelveHour,
                DisplayMode = ClockDisplayMode.Digital,
                ShowSeconds = true,
                ShowDate = true,
                DateFormat = ClockDateFormat.Full,
                HourlyChime = false,
                SyncWithNotch = true,
                WorldClocks = new List<WorldClockCity>
                {
                    new WorldClockCity { CityName = localName, CountryOrRegion = "Current Location", TimeZoneId = localTz.Id, IsLocal = true, Latitude = localLat, Longitude = localLon },
                    new WorldClockCity { CityName = "London", CountryOrRegion = "United Kingdom", TimeZoneId = "GMT Standard Time", Latitude = 51.5074, Longitude = -0.1278 },
                    new WorldClockCity { CityName = "New York", CountryOrRegion = "United States", TimeZoneId = "Eastern Standard Time", Latitude = 40.7128, Longitude = -74.0060 },
                    new WorldClockCity { CityName = "Tokyo", CountryOrRegion = "Japan", TimeZoneId = "Tokyo Standard Time", Latitude = 35.6762, Longitude = 139.6503 }
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

        public void SetFormat(ClockFormat fmt)
        {
            if (Settings.Format != fmt)
            {
                Settings.Format = fmt;
                Save();
                SettingsChanged?.Invoke();
            }
        }

        public void SetDisplayMode(ClockDisplayMode mode)
        {
            if (Settings.DisplayMode != mode)
            {
                Settings.DisplayMode = mode;
                if (Settings.SyncWithNotch)
                {
                    AppPreferences.Instance.ClosedClockStyle = mode == ClockDisplayMode.Analog ? "Analog" : "Digital";
                }
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

        public void ToggleHourlyChime()
        {
            Settings.HourlyChime = !Settings.HourlyChime;
            Save();
            SettingsChanged?.Invoke();
        }

        public void ToggleSyncWithNotch()
        {
            Settings.SyncWithNotch = !Settings.SyncWithNotch;
            Save();
            SettingsChanged?.Invoke();
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
                    CountryOrRegion = city.CountryOrRegion,
                    TimeZoneId = city.TimeZoneId,
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                    IsLocal = false
                });
                Save();
                SettingsChanged?.Invoke();
            }
        }

        public void AddCustomCity(string name, string timeZoneId, string region)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            lock (_lock)
            {
                Settings.WorldClocks.Add(new WorldClockCity
                {
                    CityName = name.Trim(),
                    CountryOrRegion = string.IsNullOrWhiteSpace(region) ? "Custom" : region.Trim(),
                    TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? TimeZoneInfo.Local.Id : timeZoneId,
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

        public void MoveWorldClock(int fromIndex, int toIndex)
        {
            lock (_lock)
            {
                if (fromIndex < 0 || fromIndex >= Settings.WorldClocks.Count ||
                    toIndex < 0 || toIndex >= Settings.WorldClocks.Count || fromIndex == toIndex)
                    return;

                var item = Settings.WorldClocks[fromIndex];
                Settings.WorldClocks.RemoveAt(fromIndex);
                Settings.WorldClocks.Insert(toIndex, item);
                Save();
                SettingsChanged?.Invoke();
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

        private static readonly ConcurrentDictionary<string, TimeZoneInfo> _tzCache = new();

        public TimeZoneInfo ResolveTimeZone(string tzId)
        {
            if (string.IsNullOrWhiteSpace(tzId)) return TimeZoneInfo.Local;
            return _tzCache.GetOrAdd(tzId, id =>
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch
                {
                    return TimeZoneInfo.Local;
                }
            });
        }

        public (DateTime time, string subtext, bool isDay) GetCityInfo(WorldClockCity city, DateTime? simulatedLocalTime = null)
        {
            DateTime utcNow = DateTime.UtcNow;
            DateTime localNow = simulatedLocalTime ?? DateTime.Now;
            TimeZoneInfo localTz = TimeZoneInfo.Local;

            DateTime cityTime;
            TimeSpan diff;
            TimeZoneInfo tz;

            if (city.IsLocal)
            {
                tz = localTz;
                cityTime = localNow;
                diff = TimeSpan.Zero;
            }
            else
            {
                tz = ResolveTimeZone(city.TimeZoneId);
                if (simulatedLocalTime.HasValue)
                {
                    TimeSpan shift = simulatedLocalTime.Value - DateTime.Now;
                    cityTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow + shift, tz);
                }
                else
                {
                    cityTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
                }

                TimeSpan cityOffset = tz.GetUtcOffset(utcNow);
                TimeSpan localOffset = localTz.GetUtcOffset(utcNow);
                diff = cityOffset - localOffset;
            }

            string abbrev = GetTimeZoneAbbrev(tz, utcNow);
            string diffStr = city.IsLocal ? "Current Location" : FormatDifference(diff, cityTime.Date, localNow.Date);
            bool isDay = cityTime.Hour >= 6 && cityTime.Hour < 18;

            return (cityTime, $"{abbrev} · {diffStr}", isDay);
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

            if (id.Contains("Iran", StringComparison.OrdinalIgnoreCase)) return "IRST";
            if (id.Contains("Eastern", StringComparison.OrdinalIgnoreCase)) return isDst ? "EDT" : "EST";
            if (id.Contains("Pacific", StringComparison.OrdinalIgnoreCase)) return isDst ? "PDT" : "PST";
            if (id.Contains("Central", StringComparison.OrdinalIgnoreCase)) return isDst ? "CDT" : "CST";
            if (id.Contains("Mountain", StringComparison.OrdinalIgnoreCase)) return isDst ? "MDT" : "MST";
            if (id.Contains("GMT", StringComparison.OrdinalIgnoreCase) || id.Contains("UTC", StringComparison.OrdinalIgnoreCase)) return "GMT";
            if (id.Contains("Tokyo", StringComparison.OrdinalIgnoreCase)) return "JST";
            if (id.Contains("Romance", StringComparison.OrdinalIgnoreCase) || id.Contains("W. Europe", StringComparison.OrdinalIgnoreCase)) return isDst ? "CEST" : "CET";
            if (id.Contains("Arabian", StringComparison.OrdinalIgnoreCase)) return "GST";
            if (id.Contains("AUS Eastern", StringComparison.OrdinalIgnoreCase)) return isDst ? "AEDT" : "AEST";
            if (id.Contains("Singapore", StringComparison.OrdinalIgnoreCase)) return "SGT";
            if (id.Contains("China", StringComparison.OrdinalIgnoreCase)) return "CST";
            if (id.Contains("Korea", StringComparison.OrdinalIgnoreCase)) return "KST";
            if (id.Contains("India", StringComparison.OrdinalIgnoreCase)) return "IST";

            return tz.StandardName.Length > 4 ? tz.StandardName.Substring(0, 3).ToUpperInvariant() : tz.StandardName;
        }

        public List<CuratedCityEntry> SearchCities(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return CuratedCities;
            }

            string q = query.Trim().ToLowerInvariant();
            var results = CuratedCities
                .Where(c => c.CityName.ToLowerInvariant().Contains(q) ||
                            c.CountryOrRegion.ToLowerInvariant().Contains(q) ||
                            c.TimeZoneId.ToLowerInvariant().Contains(q))
                .ToList();

            // Also search all Windows system time zones if not found
            if (results.Count < 5)
            {
                try
                {
                    foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
                    {
                        if (tz.DisplayName.ToLowerInvariant().Contains(q) || tz.Id.ToLowerInvariant().Contains(q))
                        {
                            if (!results.Any(r => r.TimeZoneId == tz.Id))
                            {
                                results.Add(new CuratedCityEntry
                                {
                                    CityName = tz.DisplayName.Split(')')[^1].Trim(),
                                    CountryOrRegion = tz.Id,
                                    TimeZoneId = tz.Id,
                                    FlagOrIcon = "🌐",
                                    Latitude = 30.0,
                                    Longitude = 0.0
                                });
                            }
                        }
                    }
                }
                catch { }
            }

            return results;
        }
    }
}
