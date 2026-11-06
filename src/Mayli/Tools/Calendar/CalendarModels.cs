using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Calendar
{
    public enum CalendarType
    {
        Jalali,     // شمسی
        Gregorian   // میلادی
    }

    public sealed class CalendarEventItem
    {
        public string Description { get; set; } = "";
        public string AdditionalDescription { get; set; } = "";
        public bool IsHoliday { get; set; }
        public bool IsReligious { get; set; }
        public string Source { get; set; } = "api"; // "api" or "builtin"
    }

    public sealed class DayEventsPayload
    {
        public bool IsHoliday { get; set; }
        public List<CalendarEventItem> Events { get; set; } = new();
        public DateTime Date { get; set; }
        public int JalaliYear { get; set; }
        public int JalaliMonth { get; set; }
        public int JalaliDay { get; set; }
        public int GregYear { get; set; }
        public int GregMonth { get; set; }
        public int GregDay { get; set; }
        public string HijriText { get; set; } = "";
    }

    public sealed class CalendarCellData
    {
        public DateTime Date { get; set; }
        public int PrimaryDay { get; set; }
        public int SecondaryDay { get; set; }
        public bool IsCurrentMonth { get; set; }
        public bool IsToday { get; set; }
        public bool IsSelected { get; set; }
        public bool IsWeekend { get; set; }
        public bool IsHoliday { get; set; }
        public int EventCount { get; set; }
        public List<CalendarEventItem> Events { get; set; } = new();
    }
}
