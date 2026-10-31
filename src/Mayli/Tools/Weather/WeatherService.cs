using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Weather
{
    public sealed class WeatherService
    {
        private static readonly Lazy<WeatherService> _instance = new(() => new WeatherService());
        public static WeatherService Instance => _instance.Value;

        private const string PrefsFileName = "weather_prefs.json";
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };

        public event Action WeatherUpdated;

        public WeatherPrefs Prefs { get; private set; } = new();
        public WeatherData Current { get; private set; } = new();
        public bool IsFetching { get; private set; }

        public bool UseFahrenheit
        {
            get => Prefs.UseFahrenheit;
            set
            {
                if (Prefs.UseFahrenheit != value)
                {
                    Prefs.UseFahrenheit = value;
                    Save();
                    WeatherUpdated?.Invoke();
                }
            }
        }

        private WeatherService()
        {
            Load();
            InitFallbackData();
            _ = FetchWeatherAsync();
        }

        public void Load()
        {
            try
            {
                var saved = PersistenceHelper.SafeReadJson<WeatherPrefs>(PrefsFileName);
                if (saved != null)
                {
                    Prefs = saved;
                    if (Prefs.RecentCities == null) Prefs.RecentCities = new List<GeocodedCity>();
                }
                else
                {
                    Prefs = new WeatherPrefs
                    {
                        City = "Isfahan",
                        Country = "Iran",
                        Latitude = 32.6546,
                        Longitude = 51.6680,
                        UseFahrenheit = false,
                        IsConfigured = true,
                        RecentCities = GetDefaultPopularCities()
                    };
                    Save();
                }
            }
            catch
            {
                Prefs = new WeatherPrefs();
            }
        }

        public void Save()
        {
            try
            {
                PersistenceHelper.SafeWriteJson(PrefsFileName, Prefs);
            }
            catch { }
        }

        public void SetCity(GeocodedCity city)
        {
            if (city == null) return;
            Prefs.City = city.Name;
            Prefs.Country = city.Country;
            Prefs.Latitude = city.Latitude;
            Prefs.Longitude = city.Longitude;
            Prefs.IsConfigured = true;

            // Update Recent
            Prefs.RecentCities.RemoveAll(c => string.Equals(c.Name, city.Name, StringComparison.OrdinalIgnoreCase));
            Prefs.RecentCities.Insert(0, city);
            if (Prefs.RecentCities.Count > 8) Prefs.RecentCities.RemoveAt(Prefs.RecentCities.Count - 1);

            Save();
            _ = FetchWeatherAsync();
        }

        public double ConvertTemp(double celsius)
        {
            return UseFahrenheit ? (celsius * 9.0 / 5.0) + 32.0 : celsius;
        }

        public string FormatTemp(double celsius)
        {
            return $"{Math.Round(ConvertTemp(celsius))}°";
        }

        public async Task FetchWeatherAsync()
        {
            if (IsFetching) return;
            IsFetching = true;

            try
            {
                string url = string.Format(
                    CultureInfo.InvariantCulture,
                    "https://api.open-meteo.com/v1/forecast?latitude={0:F4}&longitude={1:F4}&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,is_day,uv_index&hourly=temperature_2m,weather_code,is_day&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset,uv_index_max&timezone=auto&forecast_days=7",
                    Prefs.Latitude,
                    Prefs.Longitude);

                string json = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var data = new WeatherData
                {
                    City = Prefs.City,
                    IsConfigured = true
                };

                // Current
                if (root.TryGetProperty("current", out var current))
                {
                    data.Temperature = current.GetProperty("temperature_2m").GetDouble();
                    data.FeelsLike = current.GetProperty("apparent_temperature").GetDouble();
                    data.Humidity = current.GetProperty("relative_humidity_2m").GetInt32();
                    data.WeatherCode = current.GetProperty("weather_code").GetInt32();
                    data.WindSpeed = current.GetProperty("wind_speed_10m").GetDouble();
                    data.UvIndex = current.TryGetProperty("uv_index", out var uv) ? uv.GetDouble() : 0.0;
                    data.Condition = GetConditionText(data.WeatherCode);
                    data.UvText = GetUvDescription(data.UvIndex);
                }

                // Daily
                if (root.TryGetProperty("daily", out var daily))
                {
                    var maxArr = daily.GetProperty("temperature_2m_max");
                    var minArr = daily.GetProperty("temperature_2m_min");
                    var codeArr = daily.GetProperty("weather_code");
                    var sunriseArr = daily.GetProperty("sunrise");
                    var sunsetArr = daily.GetProperty("sunset");

                    if (maxArr.GetArrayLength() > 0) data.HighTemp = maxArr[0].GetDouble();
                    if (minArr.GetArrayLength() > 0) data.LowTemp = minArr[0].GetDouble();

                    if (sunriseArr.GetArrayLength() > 0)
                    {
                        string sr = sunriseArr[0].GetString();
                        data.Sunrise = FormatTimeIso(sr);
                    }
                    if (sunsetArr.GetArrayLength() > 0)
                    {
                        string ss = sunsetArr[0].GetString();
                        data.Sunset = FormatTimeIso(ss);
                    }

                    int count = Math.Min(5, maxArr.GetArrayLength());
                    for (int i = 0; i < count; i++)
                    {
                        var d = DateTime.Today.AddDays(i);
                        int code = codeArr[i].GetInt32();
                        data.Daily.Add(new DailyForecast
                        {
                            DayName = i == 0 ? "Today" : d.ToString("ddd", CultureInfo.InvariantCulture),
                            Date = d,
                            MinTemp = minArr[i].GetDouble(),
                            MaxTemp = maxArr[i].GetDouble(),
                            WeatherCode = code,
                            Condition = GetConditionText(code),
                            IconType = GetIconType(code, true)
                        });
                    }
                }

                // Hourly (find current hour index)
                if (root.TryGetProperty("hourly", out var hourly))
                {
                    var timeArr = hourly.GetProperty("time");
                    var tempArr = hourly.GetProperty("temperature_2m");
                    var codeArr = hourly.GetProperty("weather_code");
                    var isDayArr = hourly.GetProperty("is_day");

                    int startIndex = 0;
                    string nowPrefix = DateTime.Now.ToString("yyyy-MM-ddTHH", CultureInfo.InvariantCulture);
                    for (int i = 0; i < timeArr.GetArrayLength(); i++)
                    {
                        if (timeArr[i].GetString()?.StartsWith(nowPrefix) == true)
                        {
                            startIndex = i;
                            break;
                        }
                    }

                    for (int i = 0; i < 6; i++)
                    {
                        int idx = startIndex + i;
                        if (idx >= timeArr.GetArrayLength()) break;

                        DateTime dt = DateTime.Now.AddHours(i);
                        int code = codeArr[idx].GetInt32();
                        bool isDay = isDayArr[idx].GetInt32() == 1;

                        string label = i == 0 ? "Now" : dt.ToString("h tt", CultureInfo.InvariantCulture).ToUpperInvariant();
                        data.Hourly.Add(new HourlyForecast
                        {
                            Time = label,
                            Hour = dt.Hour,
                            Temp = tempArr[idx].GetDouble(),
                            WeatherCode = code,
                            IsDay = isDay,
                            IconType = GetIconType(code, isDay)
                        });
                    }
                }

                data.LastUpdated = DateTime.Now.ToString("H:mm", CultureInfo.InvariantCulture);
                Current = data;
            }
            catch
            {
                // Fallback already keeps solid data
            }
            finally
            {
                IsFetching = false;
                System.Windows.Application.Current?.Dispatcher?.Invoke(() => WeatherUpdated?.Invoke());
            }
        }

        public async Task<List<GeocodedCity>> SearchCitiesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Prefs.RecentCities.Count > 0 ? Prefs.RecentCities : GetDefaultPopularCities();

            try
            {
                string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query.Trim())}&count=10&language=en&format=json";
                string json = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("results", out var results))
                {
                    var list = new List<GeocodedCity>();
                    foreach (var item in results.EnumerateArray())
                    {
                        list.Add(new GeocodedCity
                        {
                            Name = item.GetProperty("name").GetString() ?? "",
                            Country = item.TryGetProperty("country", out var c) ? c.GetString() ?? "" : "",
                            Admin1 = item.TryGetProperty("admin1", out var a) ? a.GetString() ?? "" : "",
                            Latitude = item.GetProperty("latitude").GetDouble(),
                            Longitude = item.GetProperty("longitude").GetDouble()
                        });
                    }
                    return list;
                }
            }
            catch { }

            // Filter popular defaults as offline fallback
            return GetDefaultPopularCities()
                .Where(c => c.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private void InitFallbackData()
        {
            var data = new WeatherData
            {
                City = Prefs.City,
                Temperature = 18,
                FeelsLike = 15,
                HighTemp = 25,
                LowTemp = 10,
                Condition = "Overcast",
                WeatherCode = 3,
                Humidity = 43,
                WindSpeed = 11,
                UvIndex = 0,
                UvText = "0 Low",
                Sunrise = "6:19",
                Sunset = "5:13",
                LastUpdated = DateTime.Now.ToString("H:mm", CultureInfo.InvariantCulture),
                IsConfigured = true
            };

            // Hourly matching screenshot
            data.Hourly.Add(new HourlyForecast { Time = "Now", Temp = 18, WeatherCode = 3, IsDay = false, IconType = "cloud" });
            data.Hourly.Add(new HourlyForecast { Time = "11 PM", Temp = 17, WeatherCode = 3, IsDay = false, IconType = "cloud" });
            data.Hourly.Add(new HourlyForecast { Time = "12 AM", Temp = 16, WeatherCode = 3, IsDay = false, IconType = "cloud" });
            data.Hourly.Add(new HourlyForecast { Time = "1 AM", Temp = 16, WeatherCode = 3, IsDay = false, IconType = "cloud" });
            data.Hourly.Add(new HourlyForecast { Time = "2 AM", Temp = 15, WeatherCode = 2, IsDay = false, IconType = "moon_cloud" });
            data.Hourly.Add(new HourlyForecast { Time = "3 AM", Temp = 14, WeatherCode = 3, IsDay = false, IconType = "cloud" });

            // 5-Day matching screenshot
            data.Daily.Add(new DailyForecast { DayName = "Today", MinTemp = 10, MaxTemp = 25, WeatherCode = 3, Condition = "Overcast", IconType = "cloud" });
            data.Daily.Add(new DailyForecast { DayName = "Sun", MinTemp = 13, MaxTemp = 24, WeatherCode = 61, Condition = "Rain", IconType = "rain" });
            data.Daily.Add(new DailyForecast { DayName = "Mon", MinTemp = 9, MaxTemp = 23, WeatherCode = 3, Condition = "Overcast", IconType = "cloud" });
            data.Daily.Add(new DailyForecast { DayName = "Tue", MinTemp = 11, MaxTemp = 23, WeatherCode = 61, Condition = "Rain", IconType = "rain" });
            data.Daily.Add(new DailyForecast { DayName = "Wed", MinTemp = 8, MaxTemp = 22, WeatherCode = 3, Condition = "Overcast", IconType = "cloud" });

            Current = data;
        }

        private static string FormatTimeIso(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "";
            if (DateTime.TryParse(iso, out var dt))
            {
                return dt.ToString("h:mm", CultureInfo.InvariantCulture);
            }
            return iso;
        }

        public static string GetConditionText(int code)
        {
            return code switch
            {
                0 => "Clear",
                1 => "Mainly Clear",
                2 => "Partly Cloudy",
                3 => "Overcast",
                45 or 48 => "Fog",
                51 or 53 or 55 => "Drizzle",
                61 or 63 or 65 => "Rain",
                71 or 73 or 75 => "Snow",
                80 or 81 or 82 => "Showers",
                95 or 96 or 99 => "Thunderstorm",
                _ => "Overcast"
            };
        }

        public static string GetIconType(int code, bool isDay)
        {
            return code switch
            {
                0 => isDay ? "sun" : "moon",
                1 or 2 => isDay ? "sun_cloud" : "moon_cloud",
                3 => "cloud",
                45 or 48 => "fog",
                51 or 53 or 55 or 61 or 63 or 65 or 80 or 81 or 82 => "rain",
                71 or 73 or 75 => "snow",
                95 or 96 or 99 => "storm",
                _ => "cloud"
            };
        }

        private static string GetUvDescription(double uv)
        {
            if (uv < 3) return $"{Math.Round(uv)} Low";
            if (uv < 6) return $"{Math.Round(uv)} Moderate";
            if (uv < 8) return $"{Math.Round(uv)} High";
            if (uv < 11) return $"{Math.Round(uv)} Very High";
            return $"{Math.Round(uv)} Extreme";
        }

        public static List<GeocodedCity> GetDefaultPopularCities()
        {
            return new List<GeocodedCity>
            {
                new GeocodedCity { Name = "Isfahan", Country = "Iran", Admin1 = "Isfahan", Latitude = 32.6546, Longitude = 51.6680 },
                new GeocodedCity { Name = "Tehran", Country = "Iran", Admin1 = "Tehran", Latitude = 35.6892, Longitude = 51.3890 },
                new GeocodedCity { Name = "Shiraz", Country = "Iran", Admin1 = "Fars", Latitude = 29.5926, Longitude = 52.5836 },
                new GeocodedCity { Name = "Tabriz", Country = "Iran", Admin1 = "East Azerbaijan", Latitude = 38.0800, Longitude = 46.2919 },
                new GeocodedCity { Name = "Mashhad", Country = "Iran", Admin1 = "Razavi Khorasan", Latitude = 36.2970, Longitude = 59.6062 },
                new GeocodedCity { Name = "London", Country = "United Kingdom", Admin1 = "England", Latitude = 51.5074, Longitude = -0.1278 },
                new GeocodedCity { Name = "New York", Country = "United States", Admin1 = "New York", Latitude = 40.7128, Longitude = -74.0060 },
                new GeocodedCity { Name = "Tokyo", Country = "Japan", Admin1 = "Tokyo", Latitude = 35.6762, Longitude = 139.6503 }
            };
        }
    }
}
