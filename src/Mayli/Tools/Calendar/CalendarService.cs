using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Calendar
{
    public sealed class CalendarService
    {
        private static readonly Lazy<CalendarService> _lazy = new(() => new CalendarService());
        public static CalendarService Instance => _lazy.Value;

        private readonly PersianCalendar _pc = new();
        private readonly HijriCalendar _hc = new();
        private readonly HttpClient _http;
        private readonly string _cacheFilePath;

        // In-memory cache: key = "J_1405_08_15" or "G_2026_11_06"
        private readonly ConcurrentDictionary<string, DayEventsPayload> _eventsCache = new(StringComparer.OrdinalIgnoreCase);

        public CalendarType CurrentCalendarType { get; private set; } = CalendarType.Jalali;
        public DateTime SelectedDate { get; private set; } = DateTime.Today;

        public int DisplayYear { get; private set; }
        public int DisplayMonth { get; private set; }

        public event Action StateChanged;
        public event Action<DateTime> DateSelected;
        public event Action EventsUpdated;

        private static readonly string[] PersianMonthNames =
        {
            "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
            "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
        };

        private static readonly string[] PersianMonthNamesLatin =
        {
            "Farvardin", "Ordibehesht", "Khordad", "Tir", "Mordad", "Shahrivar",
            "Mehr", "Aban", "Azar", "Dey", "Bahman", "Esfand"
        };

        private static readonly string[] GregorianMonthNames =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        private static readonly string[] GregorianMonthNamesPersian =
        {
            "ژانویه", "فوریه", "مارس", "آوریل", "مه", "ژوئن",
            "ژوئیه", "اوت", "سپتامبر", "اکتبر", "نوامبر", "دسامبر"
        };

        private static readonly string[] IslamicMonthNames =
        {
            "محرم", "صفر", "ربیع‌الاول", "ربیع‌الثانی", "جمادی‌الاول", "جمادی‌الثانی",
            "رجب", "شعبان", "رمضان", "شوال", "ذیقعده", "ذیحجه"
        };

        private CalendarService()
        {
            _http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(4)
            };
            _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) MayliCalendar/1.1");
            _http.DefaultRequestHeaders.Add("Accept", "application/json");

            _cacheFilePath = Path.Combine(AppPaths.DataDir, "calendar_events_cache.json");
            LoadDiskCache();

            string savedMode = AppPreferences.Instance.CalendarSystem;
            CurrentCalendarType = string.Equals(savedMode, "gregorian", StringComparison.OrdinalIgnoreCase)
                ? CalendarType.Gregorian
                : CalendarType.Jalali;

            SyncDisplayWithSelectedDate();
        }

        private void SyncDisplayWithSelectedDate()
        {
            if (CurrentCalendarType == CalendarType.Jalali)
            {
                DisplayYear = _pc.GetYear(SelectedDate);
                DisplayMonth = _pc.GetMonth(SelectedDate);
            }
            else
            {
                DisplayYear = SelectedDate.Year;
                DisplayMonth = SelectedDate.Month;
            }
        }

        public void SwitchCalendarType(CalendarType newType)
        {
            if (CurrentCalendarType == newType) return;
            CurrentCalendarType = newType;
            AppPreferences.Instance.CalendarSystem = newType == CalendarType.Jalali ? "jalali" : "gregorian";
            SyncDisplayWithSelectedDate();
            StateChanged?.Invoke();
            _ = PrefetchMonthEventsAsync(DisplayYear, DisplayMonth, CurrentCalendarType);
        }

        public void SelectDate(DateTime date)
        {
            SelectedDate = date.Date;
            DateSelected?.Invoke(SelectedDate);
            StateChanged?.Invoke();
        }

        public void GoToToday()
        {
            SelectedDate = DateTime.Today;
            SyncDisplayWithSelectedDate();
            StateChanged?.Invoke();
            DateSelected?.Invoke(SelectedDate);
            _ = PrefetchMonthEventsAsync(DisplayYear, DisplayMonth, CurrentCalendarType);
        }

        public void NextMonth()
        {
            if (CurrentCalendarType == CalendarType.Jalali)
            {
                if (DisplayMonth == 12)
                {
                    DisplayYear++;
                    DisplayMonth = 1;
                }
                else
                {
                    DisplayMonth++;
                }
            }
            else
            {
                if (DisplayMonth == 12)
                {
                    DisplayYear++;
                    DisplayMonth = 1;
                }
                else
                {
                    DisplayMonth++;
                }
            }
            StateChanged?.Invoke();
            _ = PrefetchMonthEventsAsync(DisplayYear, DisplayMonth, CurrentCalendarType);
        }

        public void PreviousMonth()
        {
            if (CurrentCalendarType == CalendarType.Jalali)
            {
                if (DisplayMonth == 1)
                {
                    DisplayYear--;
                    DisplayMonth = 12;
                }
                else
                {
                    DisplayMonth--;
                }
            }
            else
            {
                if (DisplayMonth == 1)
                {
                    DisplayYear--;
                    DisplayMonth = 12;
                }
                else
                {
                    DisplayMonth--;
                }
            }
            StateChanged?.Invoke();
            _ = PrefetchMonthEventsAsync(DisplayYear, DisplayMonth, CurrentCalendarType);
        }

        public void AdjustYear(int delta)
        {
            DisplayYear += delta;
            StateChanged?.Invoke();
            _ = PrefetchMonthEventsAsync(DisplayYear, DisplayMonth, CurrentCalendarType);
        }

        #region Month Matrix & Dual Date Calculation

        public List<CalendarCellData> GetCurrentMonthCells()
        {
            var cells = new List<CalendarCellData>(42);
            DateTime today = DateTime.Today;

            if (CurrentCalendarType == CalendarType.Jalali)
            {
                DateTime firstDay = _pc.ToDateTime(DisplayYear, DisplayMonth, 1, 0, 0, 0, 0);
                // Saturday = 0 in Jalali
                int startCol = ((int)firstDay.DayOfWeek + 1) % 7;
                DateTime gridStart = firstDay.AddDays(-startCol);

                for (int i = 0; i < 42; i++)
                {
                    DateTime cellDate = gridStart.AddDays(i);
                    int cy = _pc.GetYear(cellDate);
                    int cm = _pc.GetMonth(cellDate);
                    int cd = _pc.GetDayOfMonth(cellDate);
                    bool isCurrentMonth = (cy == DisplayYear && cm == DisplayMonth);
                    bool isWeekend = cellDate.DayOfWeek == DayOfWeek.Friday;
                    bool isSelected = (cellDate.Date == SelectedDate.Date);
                    bool isToday = (cellDate.Date == today);

                    var payload = GetCachedEventsOrBuiltin(cellDate);

                    cells.Add(new CalendarCellData
                    {
                        Date = cellDate,
                        PrimaryDay = cd,
                        SecondaryDay = cellDate.Day,
                        IsCurrentMonth = isCurrentMonth,
                        IsToday = isToday,
                        IsSelected = isSelected,
                        IsWeekend = isWeekend,
                        IsHoliday = isWeekend || (payload?.IsHoliday ?? false),
                        EventCount = payload?.Events?.Count ?? 0,
                        Events = payload?.Events ?? new List<CalendarEventItem>()
                    });
                }
            }
            else
            {
                DateTime firstDay = new DateTime(DisplayYear, DisplayMonth, 1);
                // Monday = 0 in ISO Gregorian
                int startCol = ((int)firstDay.DayOfWeek + 6) % 7;
                DateTime gridStart = firstDay.AddDays(-startCol);

                for (int i = 0; i < 42; i++)
                {
                    DateTime cellDate = gridStart.AddDays(i);
                    bool isCurrentMonth = (cellDate.Year == DisplayYear && cellDate.Month == DisplayMonth);
                    bool isWeekend = cellDate.DayOfWeek == DayOfWeek.Sunday;
                    bool isSelected = (cellDate.Date == SelectedDate.Date);
                    bool isToday = (cellDate.Date == today);

                    int jDay = _pc.GetDayOfMonth(cellDate);
                    var payload = GetCachedEventsOrBuiltin(cellDate);

                    cells.Add(new CalendarCellData
                    {
                        Date = cellDate,
                        PrimaryDay = cellDate.Day,
                        SecondaryDay = jDay,
                        IsCurrentMonth = isCurrentMonth,
                        IsToday = isToday,
                        IsSelected = isSelected,
                        IsWeekend = isWeekend,
                        IsHoliday = isWeekend || (payload?.IsHoliday ?? false),
                        EventCount = payload?.Events?.Count ?? 0,
                        Events = payload?.Events ?? new List<CalendarEventItem>()
                    });
                }
            }

            return cells;
        }

        public string GetDisplayHeaderTitle()
        {
            if (CurrentCalendarType == CalendarType.Jalali)
            {
                string mName = PersianMonthNames[DisplayMonth - 1];
                string yStr = ToPersianDigits(DisplayYear.ToString());
                return $"{mName} {yStr}";
            }
            else
            {
                string mName = GregorianMonthNames[DisplayMonth - 1];
                string yStr = DisplayYear.ToString();
                return $"{mName} {yStr}";
            }
        }

        public string GetDisplayHeaderSubtitle()
        {
            if (CurrentCalendarType == CalendarType.Jalali)
            {
                // Spans which Gregorian months?
                DateTime start = _pc.ToDateTime(DisplayYear, DisplayMonth, 1, 0, 0, 0, 0);
                int days = _pc.GetDaysInMonth(DisplayYear, DisplayMonth);
                DateTime end = start.AddDays(days - 1);

                string sm = start.ToString("MMM", CultureInfo.InvariantCulture);
                string em = end.ToString("MMM", CultureInfo.InvariantCulture);
                string y = start.Year == end.Year ? start.Year.ToString() : $"{start.Year}-{end.Year}";
                return sm == em ? $"{sm} {y}" : $"{sm} - {em} {y}";
            }
            else
            {
                DateTime start = new DateTime(DisplayYear, DisplayMonth, 1);
                DateTime end = start.AddMonths(1).AddDays(-1);

                int sm = _pc.GetMonth(start);
                int em = _pc.GetMonth(end);
                int sy = _pc.GetYear(start);
                int ey = _pc.GetYear(end);

                string smName = PersianMonthNames[sm - 1];
                string emName = PersianMonthNames[em - 1];
                string yStr = ToPersianDigits(sy == ey ? sy.ToString() : $"{sy}-{ey}");
                return $"{smName} - {emName} {yStr}";
            }
        }

        public DayEventsPayload GetSelectedDayPayload()
        {
            return GetCachedEventsOrBuiltin(SelectedDate);
        }

        public string GetFullJalaliDate(DateTime date, bool persianDigits = true)
        {
            int y = _pc.GetYear(date);
            int m = _pc.GetMonth(date);
            int d = _pc.GetDayOfMonth(date);
            string mName = PersianMonthNames[m - 1];
            string res = $"{d} {mName} {y}";
            return persianDigits ? ToPersianDigits(res) : res;
        }

        public string GetJalaliMonthYear(DateTime date, bool persianDigits = true)
        {
            int y = _pc.GetYear(date);
            int m = _pc.GetMonth(date);
            string mName = PersianMonthNames[m - 1];
            string res = $"{mName} {y}";
            return persianDigits ? ToPersianDigits(res) : res;
        }

        public string GetFullGregorianDate(DateTime date)
        {
            return date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        }

        public string GetGregorianMonthYear(DateTime date)
        {
            return date.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        }

        public string GetHijriDate(DateTime date, bool persianDigits = true)
        {
            try
            {
                int y = _hc.GetYear(date);
                int m = _hc.GetMonth(date);
                int d = _hc.GetDayOfMonth(date);
                string mName = (m >= 1 && m <= 12) ? IslamicMonthNames[m - 1] : $"ماه {m}";
                string res = $"{d} {mName} {y}";
                return persianDigits ? ToPersianDigits(res) : res;
            }
            catch
            {
                return "";
            }
        }

        public string GetWeekdayName(DateTime date, bool isPersian)
        {
            if (isPersian)
            {
                return date.DayOfWeek switch
                {
                    DayOfWeek.Saturday => "شنبه",
                    DayOfWeek.Sunday => "یکشنبه",
                    DayOfWeek.Monday => "دوشنبه",
                    DayOfWeek.Tuesday => "سه‌شنبه",
                    DayOfWeek.Wednesday => "چهارشنبه",
                    DayOfWeek.Thursday => "پنج‌شنبه",
                    DayOfWeek.Friday => "جمعه",
                    _ => ""
                };
            }
            return date.DayOfWeek.ToString();
        }

        public static string ToPersianDigits(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input
                .Replace('0', '۰')
                .Replace('1', '۱')
                .Replace('2', '۲')
                .Replace('3', '۳')
                .Replace('4', '۴')
                .Replace('5', '۵')
                .Replace('6', '۶')
                .Replace('7', '۷')
                .Replace('8', '۸')
                .Replace('9', '۹');
        }

        #endregion

        #region API Integration & Background Prefetcher

        public async Task PrefetchMonthEventsAsync(int year, int month, CalendarType type)
        {
            try
            {
                int daysInMonth = (type == CalendarType.Jalali)
                    ? _pc.GetDaysInMonth(year, month)
                    : DateTime.DaysInMonth(year, month);

                var tasks = new List<Task>();
                for (int d = 1; d <= daysInMonth; d++)
                {
                    DateTime date = (type == CalendarType.Jalali)
                        ? _pc.ToDateTime(year, month, d, 0, 0, 0, 0)
                        : new DateTime(year, month, d);

                    string cacheKey = GetCacheKey(date);
                    if (!_eventsCache.ContainsKey(cacheKey))
                    {
                        tasks.Add(FetchDateFromApiAsync(date));
                    }
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks.Take(12)); // throttled batch
                    SaveDiskCache();
                    EventsUpdated?.Invoke();
                }
            }
            catch { }
        }

        public async Task<DayEventsPayload> GetOrFetchEventsAsync(DateTime date)
        {
            string key = GetCacheKey(date);
            if (_eventsCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var fetched = await FetchDateFromApiAsync(date);
            SaveDiskCache();
            return fetched;
        }

        private async Task<DayEventsPayload> FetchDateFromApiAsync(DateTime date)
        {
            string key = GetCacheKey(date);
            int jy = _pc.GetYear(date);
            int jm = _pc.GetMonth(date);
            int jd = _pc.GetDayOfMonth(date);

            string url = $"https://holidayapi.ir/jalali/{jy}/{jm:00}/{jd:00}";

            try
            {
                var response = await _http.GetAsync(url).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    bool isHoliday = root.TryGetProperty("is_holiday", out var hProp) && hProp.GetBoolean();
                    var list = new List<CalendarEventItem>();

                    if (root.TryGetProperty("events", out var evArr) && evArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in evArr.EnumerateArray())
                        {
                            string desc = item.TryGetProperty("description", out var dProp) ? dProp.GetString() : "";
                            string addDesc = item.TryGetProperty("additional_description", out var aProp) ? aProp.GetString() : "";
                            bool evHoliday = item.TryGetProperty("is_holiday", out var ehProp) && ehProp.GetBoolean();
                            bool evRelig = item.TryGetProperty("is_religious", out var erProp) && erProp.GetBoolean();

                            list.Add(new CalendarEventItem
                            {
                                Description = desc ?? "",
                                AdditionalDescription = addDesc ?? "",
                                IsHoliday = evHoliday,
                                IsReligious = evRelig,
                                Source = "holidayapi.ir"
                            });
                        }
                    }

                    var payload = new DayEventsPayload
                    {
                        Date = date.Date,
                        IsHoliday = isHoliday,
                        Events = list,
                        JalaliYear = jy,
                        JalaliMonth = jm,
                        JalaliDay = jd,
                        GregYear = date.Year,
                        GregMonth = date.Month,
                        GregDay = date.Day,
                        HijriText = GetHijriDate(date, false)
                    };

                    _eventsCache[key] = payload;
                    return payload;
                }
            }
            catch
            {
                // API temporarily down or offline (e.g. disk full on holidayapi.ir)
            }

            // Graceful offline fallback from built-in engine
            var fallback = BuildBuiltinDayPayload(date);
            _eventsCache[key] = fallback;
            return fallback;
        }

        private DayEventsPayload GetCachedEventsOrBuiltin(DateTime date)
        {
            string key = GetCacheKey(date);
            if (_eventsCache.TryGetValue(key, out var payload))
            {
                return payload;
            }
            return BuildBuiltinDayPayload(date);
        }

        private string GetCacheKey(DateTime date) => $"D_{date:yyyy_MM_dd}";

        private void LoadDiskCache()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    string json = File.ReadAllText(_cacheFilePath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, DayEventsPayload>>(json);
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            _eventsCache[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveDiskCache()
        {
            try
            {
                string json = JsonSerializer.Serialize(_eventsCache);
                File.WriteAllText(_cacheFilePath, json);
            }
            catch { }
        }

        #endregion

        #region Rich Built-in Iranian & International Events Engine

        private DayEventsPayload BuildBuiltinDayPayload(DateTime date)
        {
            int jy = _pc.GetYear(date);
            int jm = _pc.GetMonth(date);
            int jd = _pc.GetDayOfMonth(date);

            bool isHoliday = (date.DayOfWeek == DayOfWeek.Friday);
            var list = new List<CalendarEventItem>();

            // 1. Solar Hijri Fixed Holidays & Occasions
            if (jm == 1)
            {
                if (jd == 1) { list.Add(new CalendarEventItem { Description = "جشن نوروز / سال نو خورشیدی", IsHoliday = true }); isHoliday = true; }
                else if (jd == 2) { list.Add(new CalendarEventItem { Description = "عید نوروز", IsHoliday = true }); isHoliday = true; }
                else if (jd == 3) { list.Add(new CalendarEventItem { Description = "عید نوروز", IsHoliday = true }); isHoliday = true; }
                else if (jd == 4) { list.Add(new CalendarEventItem { Description = "عید نوروز", IsHoliday = true }); isHoliday = true; }
                else if (jd == 12) { list.Add(new CalendarEventItem { Description = "روز جمهوری اسلامی ایران", IsHoliday = true }); isHoliday = true; }
                else if (jd == 13) { list.Add(new CalendarEventItem { Description = "روز طبیعت (سیزده‌بدر)", IsHoliday = true }); isHoliday = true; }
                else if (jd == 29) { list.Add(new CalendarEventItem { Description = "روز ارتش جمهوری اسلامی ایران", IsHoliday = false }); }
            }
            else if (jm == 2)
            {
                if (jd == 1) { list.Add(new CalendarEventItem { Description = "روز بزرگداشت سعدی", IsHoliday = false }); }
                else if (jd == 2) { list.Add(new CalendarEventItem { Description = "روز زمین پاک (Earth Day)", IsHoliday = false }); }
                else if (jd == 10) { list.Add(new CalendarEventItem { Description = "روز ملی خلیج فارس", IsHoliday = false }); }
                else if (jd == 11) { list.Add(new CalendarEventItem { Description = "روز جهانی کار و کارگر", IsHoliday = false }); }
                else if (jd == 12) { list.Add(new CalendarEventItem { Description = "روز معلم / شهادت استاد مطهری", IsHoliday = false }); }
                else if (jd == 15) { list.Add(new CalendarEventItem { Description = "جشن بهاربد / روز شیراز", IsHoliday = false }); }
                else if (jd == 25) { list.Add(new CalendarEventItem { Description = "روز بزرگداشت فردوسی", IsHoliday = false }); }
                else if (jd == 28) { list.Add(new CalendarEventItem { Description = "روز بزرگداشت خیام", IsHoliday = false }); }
            }
            else if (jm == 3)
            {
                if (jd == 3) { list.Add(new CalendarEventItem { Description = "فتح خرمشهر / روز مقاومت و ایثار", IsHoliday = false }); }
                else if (jd == 14) { list.Add(new CalendarEventItem { Description = "رحلت امام خمینی", IsHoliday = true }); isHoliday = true; }
                else if (jd == 15) { list.Add(new CalendarEventItem { Description = "قیام خونین ۱۵ خرداد", IsHoliday = true }); isHoliday = true; }
            }
            else if (jm == 4)
            {
                if (jd == 1) { list.Add(new CalendarEventItem { Description = "جشن آب‌پاشونک / روز اصناف", IsHoliday = false }); }
                else if (jd == 7) { list.Add(new CalendarEventItem { Description = "شهادت آیت‌الله بهشتی و روز قوه قضاییه", IsHoliday = false }); }
                else if (jd == 14) { list.Add(new CalendarEventItem { Description = "روز قلم", IsHoliday = false }); }
            }
            else if (jm == 5)
            {
                if (jd == 8) { list.Add(new CalendarEventItem { Description = "روز بزرگداشت شیخ شهاب‌الدین سهروردی", IsHoliday = false }); }
                else if (jd == 17) { list.Add(new CalendarEventItem { Description = "روز خبرنگار", IsHoliday = false }); }
                else if (jd == 28) { list.Add(new CalendarEventItem { Description = "سالروز کودتای ۲۸ مرداد", IsHoliday = false }); }
            }
            else if (jm == 6)
            {
                if (jd == 1) { list.Add(new CalendarEventItem { Description = "روز پزشک / بزرگداشت ابن سینا", IsHoliday = false }); }
                else if (jd == 2) { list.Add(new CalendarEventItem { Description = "آغاز هفته دولت", IsHoliday = false }); }
                else if (jd == 5) { list.Add(new CalendarEventItem { Description = "روز داروسازی / بزرگداشت زکریای رازی", IsHoliday = false }); }
                else if (jd == 13) { list.Add(new CalendarEventItem { Description = "روز بزرگداشت ابوریحان بیرونی", IsHoliday = false }); }
                else if (jd == 27) { list.Add(new CalendarEventItem { Description = "روز شعر و ادب فارسی / بزرگداشت شهریار", IsHoliday = false }); }
                else if (jd == 31) { list.Add(new CalendarEventItem { Description = "آغاز هفته دفاع مقدس", IsHoliday = false }); }
            }
            else if (jm == 7)
            {
                if (jd == 1) { list.Add(new CalendarEventItem { Description = "آغاز سال تحصیلی جدید", IsHoliday = false }); }
                else if (jd == 8) { list.Add(new CalendarEventItem { Description = "روز بزرگداشت مولوی", IsHoliday = false }); }
                else if (jd == 16) { list.Add(new CalendarEventItem { Description = "روز جهانی کودک", IsHoliday = false }); }
                else if (jd == 20) { list.Add(new CalendarEventItem { Description = "روز بزرگداشت حافظ", IsHoliday = false }); }
            }
            else if (jm == 8)
            {
                if (jd == 7) { list.Add(new CalendarEventItem { Description = "روز بزرگداشت کوروش بزرگ", IsHoliday = false }); }
                else if (jd == 13) { list.Add(new CalendarEventItem { Description = "روز دانش‌آموز", IsHoliday = false }); }
                else if (jd == 24) { list.Add(new CalendarEventItem { Description = "روز کتاب و کتابخوانی", IsHoliday = false }); }
            }
            else if (jm == 9)
            {
                if (jd == 1) { list.Add(new CalendarEventItem { Description = "آذر جشن", IsHoliday = false }); }
                else if (jd == 16) { list.Add(new CalendarEventItem { Description = "روز دانشجو", IsHoliday = false }); }
                else if (jd == 30) { list.Add(new CalendarEventItem { Description = "شب یلدا / جشن شب چله", IsHoliday = false }); }
            }
            else if (jm == 10)
            {
                if (jd == 1) { list.Add(new CalendarEventItem { Description = "روز میلاد خورشید / جشن خرم‌روز", IsHoliday = false }); }
                else if (jd == 22) { list.Add(new CalendarEventItem { Description = "تشکیل شورای انقلاب", IsHoliday = false }); }
            }
            else if (jm == 11)
            {
                if (jd == 12) { list.Add(new CalendarEventItem { Description = "بازگشت امام خمینی به ایران / آغاز دهه فجر", IsHoliday = false }); }
                else if (jd == 22) { list.Add(new CalendarEventItem { Description = "پیروزی انقلاب اسلامی ایران", IsHoliday = true }); isHoliday = true; }
                else if (jd == 29) { list.Add(new CalendarEventItem { Description = "جشن سپندارمذگان / روز عشق ایرانی", IsHoliday = false }); }
            }
            else if (jm == 12)
            {
                if (jd == 5) { list.Add(new CalendarEventItem { Description = "روز مهندس / بزرگداشت خواجه نصیرالدین طوسی", IsHoliday = false }); }
                else if (jd == 15) { list.Add(new CalendarEventItem { Description = "روز درختکاری", IsHoliday = false }); }
                else if (jd == 29) { list.Add(new CalendarEventItem { Description = "روز ملی شدن صنعت نفت ایران", IsHoliday = true }); isHoliday = true; }
            }

            // 2. International Gregorian Events
            if (date.Month == 1 && date.Day == 1)
            {
                list.Add(new CalendarEventItem { Description = "آغاز سال نو میلادی (New Year)", AdditionalDescription = "1 January", IsHoliday = false });
            }
            else if (date.Month == 2 && date.Day == 14)
            {
                list.Add(new CalendarEventItem { Description = "روز ولنتاین (Valentine's Day)", AdditionalDescription = "14 February", IsHoliday = false });
            }
            else if (date.Month == 3 && date.Day == 8)
            {
                list.Add(new CalendarEventItem { Description = "روز جهانی زن (International Women's Day)", AdditionalDescription = "8 March", IsHoliday = false });
            }
            else if (date.Month == 12 && date.Day == 25)
            {
                list.Add(new CalendarEventItem { Description = "جشن کریسمس (Christmas Day)", AdditionalDescription = "25 December", IsHoliday = false });
            }

            // 3. Islamic Lunar Holidays
            try
            {
                int hm = _hc.GetMonth(date);
                int hd = _hc.GetDayOfMonth(date);

                if (hm == 1 && hd == 9) { list.Add(new CalendarEventItem { Description = "تاسوعای حسینی", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 1 && hd == 10) { list.Add(new CalendarEventItem { Description = "عاشورای حسینی", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 2 && hd == 20) { list.Add(new CalendarEventItem { Description = "اربعین حسینی", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 2 && hd == 28) { list.Add(new CalendarEventItem { Description = "رحلت پیامبر اکرم و شهادت امام حسن مجتبی", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 2 && hd == 30) { list.Add(new CalendarEventItem { Description = "شهادت امام رضا (ع)", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 3 && hd == 17) { list.Add(new CalendarEventItem { Description = "میلاد پیامبر اکرم و امام جعفر صادق", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 6 && hd == 3) { list.Add(new CalendarEventItem { Description = "شهادت حضرت فاطمه زهرا (س)", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 7 && hd == 13) { list.Add(new CalendarEventItem { Description = "ولادت امام علی (ع) / روز پدر", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 7 && hd == 27) { list.Add(new CalendarEventItem { Description = "مبعث رسول اکرم (ص)", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 8 && hd == 15) { list.Add(new CalendarEventItem { Description = "ولادت حضرت مهدی (عج) / نیمه شعبان", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 9 && hd == 21) { list.Add(new CalendarEventItem { Description = "شهادت امام علی (ع)", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 10 && hd == 1) { list.Add(new CalendarEventItem { Description = "عید سعید فطر", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 10 && hd == 2) { list.Add(new CalendarEventItem { Description = "تعطیل به مناسبت عید سعید فطر", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 10 && hd == 25) { list.Add(new CalendarEventItem { Description = "شهادت امام جعفر صادق (ع)", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 12 && hd == 10) { list.Add(new CalendarEventItem { Description = "عید سعید قربان", IsHoliday = true, IsReligious = true }); isHoliday = true; }
                else if (hm == 12 && hd == 18) { list.Add(new CalendarEventItem { Description = "عید سعید غدیر خم", IsHoliday = true, IsReligious = true }); isHoliday = true; }
            }
            catch { }

            // Add standard Friday note if holiday has no specific events
            if (date.DayOfWeek == DayOfWeek.Friday && list.Count == 0)
            {
                list.Add(new CalendarEventItem { Description = "جمعه (تعطیل پایان هفته)", IsHoliday = true });
            }

            return new DayEventsPayload
            {
                Date = date.Date,
                IsHoliday = isHoliday,
                Events = list,
                JalaliYear = jy,
                JalaliMonth = jm,
                JalaliDay = jd,
                GregYear = date.Year,
                GregMonth = date.Month,
                GregDay = date.Day,
                HijriText = GetHijriDate(date, false)
            };
        }

        #endregion
    }
}
