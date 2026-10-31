using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Weather
{
    public sealed class WeatherPrefs
    {
        public string City { get; set; } = "Isfahan";
        public string Country { get; set; } = "Iran";
        public double Latitude { get; set; } = 32.6546;
        public double Longitude { get; set; } = 51.6680;
        public bool UseFahrenheit { get; set; } = false;
        public bool IsConfigured { get; set; } = true;
        public List<GeocodedCity> RecentCities { get; set; } = new();
    }

    public sealed class GeocodedCity
    {
        public string Name { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Admin1 { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(Admin1)
            ? $"{Name}, {Country}"
            : $"{Name}, {Admin1}, {Country}";
    }

    public sealed class HourlyForecast
    {
        public string Time { get; set; } = string.Empty;
        public int Hour { get; set; }
        public double Temp { get; set; }
        public int WeatherCode { get; set; }
        public bool IsDay { get; set; } = true;
        public string IconType { get; set; } = "cloud";
    }

    public sealed class DailyForecast
    {
        public string DayName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public double MinTemp { get; set; }
        public double MaxTemp { get; set; }
        public int WeatherCode { get; set; }
        public string Condition { get; set; } = "Clear";
        public string IconType { get; set; } = "cloud";
    }

    public sealed class WeatherData
    {
        public string City { get; set; } = "Isfahan";
        public double Temperature { get; set; } = 18;
        public double FeelsLike { get; set; } = 15;
        public double HighTemp { get; set; } = 25;
        public double LowTemp { get; set; } = 10;
        public string Condition { get; set; } = "Overcast";
        public int WeatherCode { get; set; } = 3;
        public int Humidity { get; set; } = 43;
        public double WindSpeed { get; set; } = 11;
        public double UvIndex { get; set; } = 0;
        public string UvText { get; set; } = "0 Low";
        public string Sunrise { get; set; } = "6:19";
        public string Sunset { get; set; } = "5:13";
        public string LastUpdated { get; set; } = "10:22";
        public List<HourlyForecast> Hourly { get; set; } = new();
        public List<DailyForecast> Daily { get; set; } = new();
        public bool IsConfigured { get; set; } = true;
    }
}
