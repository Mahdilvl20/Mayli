using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Clock
{
    public enum ClockFormat
    {
        TwelveHour,
        TwentyFourHour,
        System
    }

    public enum ClockDisplayMode
    {
        Digital,
        Analog,
        Dual
    }

    public enum ClockDateFormat
    {
        Full,
        Short,
        Compact
    }

    public sealed class WorldClockCity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string CityName { get; set; } = string.Empty;
        public string CountryOrRegion { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;
        public bool IsLocal { get; set; } = false;
        public double Latitude { get; set; } = 0.0;
        public double Longitude { get; set; } = 0.0;
    }

    public sealed class ClockSettingsModel
    {
        public int SchemaVersion { get; set; } = 2;
        public ClockFormat Format { get; set; } = ClockFormat.TwelveHour;
        public ClockDisplayMode DisplayMode { get; set; } = ClockDisplayMode.Digital;
        public bool ShowSeconds { get; set; } = false;
        public bool ShowDate { get; set; } = true;
        public ClockDateFormat DateFormat { get; set; } = ClockDateFormat.Full;
        public bool HourlyChime { get; set; } = false;
        public bool SyncWithNotch { get; set; } = true;
        public List<WorldClockCity> WorldClocks { get; set; } = new();
    }

    public sealed class CuratedCityEntry
    {
        public string CityName { get; set; } = string.Empty;
        public string CountryOrRegion { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;
        public string FlagOrIcon { get; set; } = "🌐";
        public double Latitude { get; set; } = 0.0;
        public double Longitude { get; set; } = 0.0;
    }
}
