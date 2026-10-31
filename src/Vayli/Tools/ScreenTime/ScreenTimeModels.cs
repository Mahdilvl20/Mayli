using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace WindowsBuddy.Tools.ScreenTime
{
    public sealed class DayScreenTimeRecord
    {
        public string DateString { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
        public int TotalSeconds { get; set; } = 0;
        public Dictionary<string, int> AppSeconds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int[] HourlySeconds { get; set; } = new int[24];
    }

    public sealed class ScreenTimeHistoryModel
    {
        public bool IsTrackingPaused { get; set; } = false;
        public int DailyLimitSeconds { get; set; } = 7 * 3600; // Default 7 hours = 25200s
        public bool NotifyOnLimitReached { get; set; } = true;
        public bool ShowNotchAlertOnLimit { get; set; } = true;
        public string LastLimitAlertDate { get; set; } = "";
        public List<DayScreenTimeRecord> Days { get; set; } = new();
        public Dictionary<string, string> ExePaths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class AppUsageItem
    {
        public string Name { get; set; }
        public int Seconds { get; set; }
        public double Percentage { get; set; }
        public string ExePath { get; set; }
        public ImageSource IconSource { get; set; }

        public string DurationFormatted => ScreenTimeService.FormatDuration(Seconds);
        public string PercentageFormatted => $"{Math.Round(Percentage)}%";
    }

    public sealed class DayBarData
    {
        public string DayName { get; set; }      // "Sun", "Mon", etc.
        public DateTime Date { get; set; }
        public int TotalSeconds { get; set; }
        public bool IsToday { get; set; }
        public double HeightFraction { get; set; } // 0.0 to 1.0 relative to max day
        public string DurationFormatted => ScreenTimeService.FormatDuration(TotalSeconds);
    }
}
