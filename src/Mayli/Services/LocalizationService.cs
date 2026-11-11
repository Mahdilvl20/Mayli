using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace WindowsBuddy.Services
{
    public static class LocalizationService
    {
        private static FontFamily _persianFont;
        public static FontFamily PersianFontFamily
        {
            get
            {
                if (_persianFont == null)
                {
                    try
                    {
                        string dummy = System.IO.Packaging.PackUriHelper.UriSchemePack;
                        _persianFont = new FontFamily("pack://application:,,,/Resources/#Vazirmatn, Vazirmatn, Vazir, Segoe UI Emoji, Segoe UI Symbol, Segoe UI");
                    }
                    catch
                    {
                        _persianFont = new FontFamily("Vazirmatn, Vazir, Segoe UI Emoji, Segoe UI Symbol, Segoe UI");
                    }
                }
                return _persianFont;
            }
        }

        private static FontFamily _latinFont;
        public static FontFamily LatinFontFamily
        {
            get
            {
                if (_latinFont == null)
                {
                    try
                    {
                        string dummy = System.IO.Packaging.PackUriHelper.UriSchemePack;
                        _latinFont = new FontFamily("pack://application:,,,/Resources/#Inter, Inter, Segoe UI Emoji, Segoe UI Symbol, Segoe UI, sans-serif");
                    }
                    catch
                    {
                        _latinFont = new FontFamily("Inter, Segoe UI Emoji, Segoe UI Symbol, Segoe UI, sans-serif");
                    }
                }
                return _latinFont;
            }
        }

        public static FontFamily CurrentFontFamily => LatinFontFamily;

        public static FlowDirection CurrentFlowDirection => FlowDirection.LeftToRight;

        public static bool IsRtl(string text)
        {
            return false;
        }

        public static event Action LanguageChanged;

        private static readonly Dictionary<string, string> _fa = new(StringComparer.OrdinalIgnoreCase)
        {
            // General Page
            ["general.title"] = "عمومی",
            ["general.subtitle"] = "راه‌اندازی، کلید میانبر، زمان و واحدها",
            ["general.startup_title"] = "اجرای میلی هنگام بالا آمدن ویندوز",
            ["general.startup_desc"] = "با هر بار ورود به حساب کاربری، میلی به‌صورت خودکار در پس‌زمینه اجرا می‌شود. بستن برنامه فقط نشست فعلی را می‌بندد.",
            ["general.shortcut_title"] = "باز کردن ناچ",
            ["general.shortcut_desc"] = "باز و بسته کردن ناچ از هر محیط و برنامه‌ای با کلید میانبر سراسری",
            ["general.change"] = "تغییر",
            ["general.press_keys"] = "کلیدها را بزنید...",
            ["general.time_title"] = "فرمت زمان",
            ["general.time_desc"] = "استفاده در ناچ، خانه و ساعت",
            ["general.time.system"] = "سیستم",
            ["general.time.12h"] = "۱۲ ساعته",
            ["general.time.24h"] = "۲۴ ساعته",
            ["general.temp_title"] = "واحد دما",
            ["general.temp_desc"] = "استفاده در آب‌وهوا، خانه و ناچ",
            ["general.wind_title"] = "سرعت باد",
            ["general.wind_desc"] = "استفاده در آب‌وهوا و خانه",
            ["general.lang_title"] = "زبان",
            ["general.lang_desc"] = "زبان رابط کاربری برنامه (فقط انگلیسی و فارسی)",

            // Navigation
            ["nav.general"] = "عمومی",
            ["nav.notch"] = "ناچ",
            ["nav.appearance"] = "ظاهر",
            ["nav.modules"] = "ماژول‌ها",
            ["nav.live_activities"] = "فعالیت‌های زنده",
            ["nav.privacy"] = "حریم خصوصی",
            ["nav.updates"] = "بروزرسانی‌ها",
            ["nav.about"] = "درباره",
            ["nav.search"] = "جستجو...",
            ["nav.subtitle"] = "ناچ هوشمند برای ویندوز",
            ["nav.help"] = "راهنما",
            ["nav.feedback"] = "بازخورد",

            // Updates
            ["updates.title"] = "بروزرسانی‌ها",
            ["updates.subtitle"] = "اطلاعات نسخه و کانال‌های انتشار",
            ["updates.installed_title"] = "نسخه نصب‌شده",
            ["updates.installed_desc"] = "میلی نسخه {0} (معماری ویندوز ۶۴ بیتی)",
            ["updates.up_to_date"] = "✓ به روز است (نسخه {0})",
            ["updates.available"] = "🚀 نسخه جدید در دسترس است: نسخه {0}",
            ["updates.check_title"] = "بررسی بروزرسانی",
            ["updates.check_desc"] = "بررسی انتشارهای جدید و پچ‌ها در گیت‌هاب",
            ["updates.check_now"] = "بررسی اکنون",
            ["updates.checking"] = "در حال بررسی...",
            ["updates.on_latest"] = "شما روی آخرین نسخه‌اید!",
            ["updates.download_btn"] = "دانلود نسخه {0} ↗",
            ["updates.channel_title"] = "کانال انتشار",
            ["updates.channel_desc"] = "انتخاب بین نسخه پایدار یا قابلیت‌های آزمایشی بتا",
            ["updates.channel_stable"] = "پایدار",
            ["updates.channel_beta"] = "بتا",

            // Tools Titles
            ["tool.home"] = "خانه",
            ["tool.system"] = "سیستم",
            ["tool.clipboard"] = "کلیپ‌بورد",
            ["tool.notes"] = "یادداشت‌ها",
            ["tool.focus"] = "زمان‌سنج و تمرکز",
            ["tool.fileshelf"] = "قفسه فایل",
            ["tool.weather"] = "آب‌وهوا",
            ["tool.clock"] = "ساعت",
            ["tool.calendar"] = "تقویم",
            ["dock.calendar"] = "تقویم",
            ["sub.calendar"] = "تقویم شمسی و میلادی · مناسبت‌ها و تعطیلات",
            ["cal.shamsi"] = "شمسی",
            ["cal.gregorian"] = "میلادی",
            ["cal.today"] = "امروز",
            ["cal.holiday"] = "تعطیل رسمی",
            ["cal.working_day"] = "روز کاری",
            ["cal.events_title"] = "مناسبت‌ها و رویدادها",
            ["cal.no_events"] = "مناسبت خاصی برای این روز ثبت نشده است",
            ["cal.copy_date"] = "کپی تاریخ",
            ["cal.copied_hint"] = "تاریخ کپی شد",
            ["cal.source_api"] = "holidayapi.ir",
            ["cal.source_builtin"] = "پایگاه داده داخلی",
            ["tool.maylidrop"] = "انتقال فایل",
            ["tool.units"] = "تبدیل واحدها",
            ["tool.links"] = "پیوندها",
            ["tool.message"] = "پیام ناچ",
            ["tool.screentime"] = "مدت استفاده (اسکرین تایم)",
            ["tool.nowplaying"] = "در حال پخش",
            ["tool.water"] = "نوشیدن آب",
            ["tool.stopwatch"] = "کرونومتر",

            // Modules Page
            ["modules.title"] = "ماژول‌ها",
            ["modules.subtitle"] = "انتخاب ابزارهای فعال در ناچ و مرتب‌سازی آنها",
            ["modules.dock.header"] = "نوار ابزار (DOCK)",
            ["modules.sec.essentials"] = "ابزارهای اصلی",
            ["modules.sec.productivity"] = "بهره‌وری و کارایی",
            ["modules.sec.insights"] = "گزارش‌ها و بینش‌ها",
            ["modules.sec.homewidgets"] = "ویجت‌های صفحه اصلی",
            ["modules.reset_order"] = "بازنشانی ترتیب",
            ["modules.tip"] = "نکته: با تغییر وضعیت هر ماژول، آیکون آن بلافاصله در ناچ اعمال می‌شود.",
            ["modules.home.desc"] = "در حال پخش، رویداد بعدی و آمار سریع",
            ["modules.system.desc"] = "پردازنده، رم، حافظه دیسک و باتری",
            ["modules.focus.desc"] = "پومودورو، شمارش معکوس، کرونومتر و نوشیدن آب",
            ["modules.weather.desc"] = "وضعیت فعلی، پیش‌بینی ساعتی و ۵ روزه",
            ["modules.clock.desc"] = "ساعت‌های جهانی، دیجیتال یا آنالوگ",
            ["modules.calendar.desc"] = "برنامه روزانه، مناسبت‌ها و یادآورها",
            ["modules.clipboard.desc"] = "تاریخچه موارد کپی‌شده با جستجوی سریع",
            ["modules.notes.desc"] = "یادداشت‌های سریع با ذخیره‌سازی خودکار",
            ["modules.fileshelf.desc"] = "طاقچه فایل برای جابه‌جایی و درگ فایل‌ها",
            ["modules.units.desc"] = "طول، وزن، دما، ارز و موارد دیگر",
            ["modules.links.desc"] = "نشانک‌ها و جستجوی سریع در وب",
            ["modules.message.desc"] = "نمایش متن و پیام‌های دلخواه روی ناچ",
            ["modules.screentime.desc"] = "گزارش زمان صرف‌شده روی برنامه‌ها",

            // Tool Subtitles
            ["sub.system"] = "باتری · شبکه · صدا · عملکرد",
            ["sub.clock"] = "ساعت محلی و ساعت‌های جهانی",
            ["sub.units"] = "تبدیل واحدهای اندازه‌گیری",
            ["sub.fileshelf"] = "فایل‌های سنجاق‌شده روی میز کار",
            ["sub.links"] = "نشانک‌ها و لینک‌های سریع",
            ["sub.message"] = "نمایش پیام متنی در نوار ناچ",
            ["sub.focus"] = "تمرکز پومودورو و فواصل زمانی",
            ["sub.clipboard_fmt"] = "{0} مورد ذخیره‌شده",
            ["sub.notes_fmt"] = "{0} یادداشت",

            // Dock Tooltips
            ["dock.home"] = "خانه",
            ["dock.system"] = "سیستم",
            ["dock.clipboard"] = "کلیپ‌بورد",
            ["dock.notes"] = "یادداشت‌ها",
            ["dock.focus"] = "زمان‌سنج و تمرکز",
            ["dock.fileshelf"] = "قفسه فایل",
            ["dock.weather"] = "آب‌وهوا",
            ["dock.clock"] = "ساعت",
            ["dock.units"] = "تبدیل واحدها",
            ["dock.links"] = "پیوندها",
            ["dock.message"] = "پیام در ناچ",
            ["dock.screentime"] = "اسکرین تایم",
            ["dock.customize"] = "افزودن و سفارشی‌سازی ابزارها",
            ["screentime.tab_7d"] = "۷ روز",
            ["screentime.tab_30d"] = "۳۰ روز",
            ["screentime.last30days"] = "۳۰ روز گذشته",
            ["screentime.click_inspect"] = "برای جزئیات روی هر خانه کلیک کنید",
            ["screentime.no_activity"] = "بدون فعالیت",

            // Header Buttons
            ["header.close"] = "بستن (Esc)",
            ["header.settings"] = "تنظیمات",
            ["header.theme_format"] = "پوسته: {0} (کلیک برای تغییر)",
            ["header.orb"] = "میلی 🐾 (کلیک برای ابراز احساسات)",

            // Tray Menu
            ["tray.toggle"] = "باز و بسته کردن ناچ",
            ["tray.settings"] = "تنظیمات...",
            ["tray.mood"] = "حالت گوگولی میلی 🐾",
            ["tray.cycle_mood"] = "تغییر حالت بعدی",
            ["tray.exit"] = "خروج از میلی",

            // Home Tool
            ["home.screentime"] = "مدت استفاده",
            ["home.system"] = "سیستم",
            ["home.weather"] = "آب‌وهوا",
            ["home.focus"] = "تمرکز",
            ["home.timer"] = "تایمر",
            ["home.clipboard"] = "کلیپ‌بورد",
            ["home.notes"] = "یادداشت‌ها",
            ["home.nowplaying"] = "در حال پخش",
            ["home.nomedia"] = "موسیقی در حال پخش نیست",
            ["home.no_apps"] = "امروز برنامه‌ای ثبت نشده",
            ["home.focusing"] = "در حال تمرکز",
            ["home.paused"] = "متوقف شده",
            ["home.start_focus"] = "شروع تمرکز",
            ["home.no_clips"] = "هنوز موردی در کلیپ‌بورد نیست",
            ["home.no_notes"] = "هنوز یادداشتی ثبت نشده",

            // System Tool
            ["system.battery"] = "باتری",
            ["system.ac_power"] = "برق مستقیم",
            ["system.desktop_pc"] = "کامپیوتر رومیزی",
            ["system.connected"] = "متصل",
            ["system.network"] = "شبکه",
            ["system.performance"] = "عملکرد",
            ["system.volume"] = "بلندی صدا",
            ["system.health_fmt"] = "سلامت {0}٪",
            ["system.fully_charged"] = "شارژ کامل",
            ["system.charging"] = "در حال شارژ",
            ["system.on_battery"] = "روی باتری",
            ["system.muted"] = "بی‌صدا",

            // Notes Tool
            ["notes.all_notes"] = "همه یادداشت‌ها",
            ["notes.no_notes"] = "هنوز یادداشتی نیست",
            ["notes.new_note"] = "یادداشت جدید",
            ["notes.search"] = "جستجوی یادداشت‌ها...",
            ["notes.empty_prompt"] = "یک یادداشت بسازید یا انتخاب کنید",
            ["notes.words"] = "کلمه",
            ["notes.edited_now"] = "هم‌اکنون ویرایش شد",

            // Clipboard Tool
            ["clipboard.today"] = "امروز",
            ["clipboard.search"] = "جستجو در کلیپ‌بورد...",
            ["clipboard.copy"] = "کپی",
            ["clipboard.copied"] = "کپی شد! ✓",
            ["clipboard.no_items"] = "موردی یافت نشد",
            ["clipboard.open_link"] = "باز کردن پیوند",

            // Screen Time Tool
            ["screentime.today"] = "امروز",
            ["screentime.title"] = "مدت استفاده",
            ["screentime.screen_time"] = "مدت استفاده",
            ["screentime.apps_used"] = "برنامه‌های استفاده‌شده",
            ["screentime.last7days"] = "۷ روز گذشته",
            ["screentime.by_app"] = "بر اساس برنامه",
            ["screentime.avg_fmt"] = "میانگین {0}",
            ["screentime.no_activity"] = "امروز هنوز فعالیتی ثبت نشده است.",
            ["screentime.refresh_tooltip"] = "تازه‌سازی فعالیت برنامه‌ها",

            // Weather Tool
            ["weather.hourly"] = "ساعتی",
            ["weather.5day"] = "۵ روزه",
            ["weather.back"] = "بازگشت",
            ["weather.select_city"] = "انتخاب شهر",
            ["weather.search_city"] = "جستجوی شهر (مثلاً تهران، اصفهان، مشهد، لندن...)",
            ["weather.popular"] = "شهرهای پرطرفدار و اخیر",
            ["weather.no_cities"] = "شهری پیدا نشد. نام دیگری را جستجو کنید.",
            ["weather.humidity"] = "رطوبت",
            ["weather.wind"] = "باد",
            ["weather.uv"] = "شاخص فرابنفش",
            ["weather.sunrise"] = "طلوع",
            ["weather.sunset"] = "غروب",
            ["weather.feels_like"] = "احساس واقعی",
            ["weather.updated_fmt"] = "بروزرسانی {0}",

            // Focus / Timers Tool
            ["focus.focus"] = "تمرکز",
            ["focus.break"] = "استراحت",
            ["focus.short_break"] = "استراحت کوتاه",
            ["focus.long_break"] = "استراحت طولانی",
            ["focus.auto_start"] = "شروع خودکار دور بعدی",
            ["focus.completed_today_fmt"] = "{0} پومودورو امروز تکمیل شد",
            ["focus.focus_min_fmt"] = "تمرکز {0} دقیقه",
            ["focus.break_min_fmt"] = "استراحت {0} دقیقه",
            ["focus.long_min_fmt"] = "طولانی {0} دقیقه",
            ["focus.cycles_fmt"] = "{0} دور",
            ["focus.cycle_header_fmt"] = "{0} {1} از {2}",
            ["focus.next_break_fmt"] = "استراحت بعدی {0}",

            // File Shelf Tool
            ["shelf.shelves"] = "قفسه‌ها",
            ["shelf.new_shelf"] = "+ قفسه جدید",
            ["shelf.empty"] = "خالی",
            ["shelf.items_fmt"] = "{0} مورد · {1}",
            ["shelf.choose_files"] = "انتخاب فایل‌ها",
            ["shelf.empty_desc"] = "هنوز فایلی در میز کار نیست — فایل‌ها را اینجا رها کنید",

            // Clock Tool
            ["clock.clock"] = "ساعت",
            ["clock.time_format"] = "فرمت زمان",
            ["clock.show_seconds"] = "نمایش ثانیه",
            ["clock.show_date"] = "نمایش تاریخ",
            ["clock.date_style"] = "استایل تاریخ",
            ["clock.change"] = "تغییر",
            ["clock.world_clocks"] = "ساعت‌های جهانی",
            ["clock.add_city"] = "+ افزودن شهر",
            ["clock.add_world_clock"] = "افزودن ساعت جهانی",

            // Units Tool
            ["units.from"] = "از",
            ["units.to"] = "به",
            ["units.recent"] = "اخیر",

            // Links Tool
            ["links.search"] = "جستجو",
            ["links.add_link"] = "+ افزودن پیوند",
            ["links.saved_links"] = "پیوندهای ذخیره‌شده",
            ["links.add_bookmark"] = "افزودن نشانک",
            ["links.links_count_fmt"] = "{0} پیوند",

            // Message Tool
            ["message.live_preview"] = "پیش‌نمایش زنده",
            ["message.placeholder"] = "متن پیام را بنویسید...",
            ["message.flash"] = "نمایش در ناچ",
            ["message.clear"] = "پاک کردن",
            ["message.take_down"] = "حذف پیام از ناچ",
            ["message.speed"] = "سرعت",
            ["message.speed.slow"] = "آهسته",
            ["message.speed.normal"] = "معمولی",
            ["message.speed.fast"] = "سریع",
            ["message.repeat"] = "تکرار تا حذف دستی",
            ["message.animation"] = "انیمیشن",
            ["message.anim.slide"] = "اسلاید",
            ["message.anim.fade"] = "محو شدن",
            ["message.anim.typeon"] = "تایپ زنده",
            ["message.anim.scroll"] = "طوماری",
            ["message.anim.blur"] = "تاری",
            ["message.anim.pulse"] = "ضربان",
            ["message.color"] = "رنگ",
            ["message.finish"] = "جنس روکش",
            ["message.finish.solid"] = "ساده",
            ["message.finish.silver"] = "نقره‌ای",
            ["message.finish.ocean"] = "اقیانوس",
            ["message.finish.aurora"] = "شفق قطبی",
            ["message.duration"] = "مدت زمان",
            ["message.shows_fmt"] = "نمایش {0} ثانیه",
            ["message.pinned"] = "سنجاق‌شده",
            ["message.custom"] = "سفارشی",
            ["message.custom_sec"] = "ثانیه",

            // Notch Settings Page
            ["notch.title"] = "ناچ",
            ["notch.subtitle"] = "شکل ظاهری، رفتار هوشمند و پیش‌نمایش زنده",
            ["notch.preview.resting"] = "حالت استراحت",
            ["notch.preview.hover"] = "نگه داشتن موس",
            ["notch.sec.visibility"] = "حالت نمایش (VISIBILITY)",
            ["notch.vis.always.title"] = "همیشه (Always)",
            ["notch.vis.always.desc"] = "همیشه در بالای صفحه نمایش داده می‌شود. قوانین تمام‌صفحه همچنان اعمال می‌شوند.",
            ["notch.vis.smart.title"] = "هوشمند (Smart)",
            ["notch.vis.smart.desc"] = "نمایش کامل در دسکتاپ؛ جمع شدن خودکار روی برنامه‌های تمام‌صفحه (Maximized).",
            ["notch.vis.minimal.title"] = "مینیمال (Minimal)",
            ["notch.vis.minimal.desc"] = "فقط یک فلش کوچک در بالای صفحه؛ با نگه داشتن موس پیش‌نمایش می‌دهد و با کلیک باز می‌شود.",
            ["notch.vis.hidden.title"] = "مخفی (Hidden)",
            ["notch.vis.hidden.desc"] = "هیچ چیزی روی صفحه نیست؛ از طریق آیکون سینی سیستم یا کلید میانبر باز می‌شود.",
            ["notch.sec.style"] = "سبک ظاهری (STYLE)",
            ["notch.style.line.title"] = "خطی (Line)",
            ["notch.style.line.desc"] = "یک نوار باریک و تخت در لبه بالایی صفحه",
            ["notch.style.notch.title"] = "ناچ (Notch)",
            ["notch.style.notch.desc"] = "متصل به لبه بالایی مانیتور با گوشه‌های منحنی",
            ["notch.style.island.title"] = "جزیره (Island)",
            ["notch.style.island.desc"] = "کپسول شناور کمی پایین‌تر از لبه بالایی صفحه",
            ["notch.shape.title"] = "قالب ناچ",
            ["notch.shape.desc"] = "تنظیم دقیق انحنا و ابعاد سبک ناچ",
            ["notch.size.title"] = "اندازه",
            ["notch.size.desc"] = "مقیاس بزرگ‌نمایی ناچ در حالت بسته",
            ["notch.layout.title"] = "چیدمان حالت استراحت",
            ["notch.layout.desc"] = "قرارگیری ساعت و باتری در گوشه‌ها یا در مرکز",
            ["notch.layout.corners"] = "گوشه‌ها",
            ["notch.layout.center"] = "مرکز",
            ["notch.opens.title"] = "نحوه باز شدن",
            ["notch.opens.desc"] = "باز شدن فوری با رفتن موس روی ناچ، یا فقط با کلیک کردن",
            ["notch.opens.hover"] = "نگه داشتن موس",
            ["notch.opens.click"] = "کلیک",
            ["notch.display.title"] = "نمایشگر",
            ["notch.display.desc"] = "مانیتوری که ناچ روی آن نمایش داده می‌شود",
            ["notch.shownotch.title"] = "نمایش ناچ",
            ["notch.shownotch.desc"] = "مخفی کردن موقت ناچ بدون بستن برنامه؛ از طریق آیکون سینی بازمی‌گردد",
            ["notch.sec.behavior"] = "رفتار (BEHAVIOR)",
            ["notch.fullscreen.title"] = "مخفی شدن در حالت تمام‌صفحه",
            ["notch.fullscreen.desc"] = "هنگام اجرای فیلم، بازی و برنامه‌های تمام‌صفحه روی مانیتور ناچ",
            ["notch.events.title"] = "نمایش رویدادها روی برنامه‌ها",
            ["notch.events.desc"] = "وقتی ناچ جمع یا مخفی است، برای رویدادهایی مثل صدا، تایمر یا آهنگ جدید موقتاً باز شود",
            ["notch.tabanim.title"] = "انیمیشن جابجایی تب‌ها",
            ["notch.tabanim.desc"] = "حرکت نرم هنگام تعویض ابزارها در داخل ناچ باز",
            ["notch.sec.pet"] = "حیوان خانگی میلی (آزمایشی)",
            ["notch.pet.title"] = "میلی پت (Beta)",
            ["notch.pet.desc"] = "همراه کوچک و بامزه در داخل ناچ شما",
            ["notch.sec.edgelight"] = "نور لبه (EDGE LIGHT)",
            ["notch.edgelight.title"] = "نور لبه ناچ",
            ["notch.edgelight.desc"] = "نور متحرک درخشان در امتداد لبه‌های ناچ",
            ["notch.edge.comet.title"] = "شهاب‌سنگ (Comet)",
            ["notch.edge.comet.desc"] = "حرکت روان نور از راست به چپ و سپس از چپ به راست",
            ["notch.edge.twin.title"] = "دو شهاب‌سنگ (Twin comets)",
            ["notch.edge.twin.desc"] = "دو نور از طرفین در پایین به هم می‌رسند و برمی‌گردند",
            ["notch.edge.breathe.title"] = "تنفس نوری (Breathe)",
            ["notch.edge.breathe.desc"] = "کل خط دور ناچ به آرامی می‌درخشد و ضربان دارد",
            ["notch.edge.color"] = "رنگ",
            ["notch.edge.custom"] = "● سفارشی...",
            ["notch.edge.sync_music"] = "🎵 همرنگ با آهنگ هنگام پخش",
            ["notch.edge.intensity"] = "شدت روشنایی",
            ["notch.edge.speed"] = "سرعت",
            ["notch.edge.speed.slow"] = "آهسته",
            ["notch.edge.speed.normal"] = "معمولی",
            ["notch.edge.speed.fast"] = "سریع",
            ["notch.edge.when.title"] = "زمان نمایش",
            ["notch.edge.when.desc"] = "در حالت «هنگام رویداد و موس»، نور چند ثانیه می‌درخشد و سپس محو می‌شود",
            ["notch.edge.when.always"] = "همیشه",
            ["notch.edge.when.events"] = "هنگام رویداد و موس",
            ["notch.sec.closed"] = "هنگام بسته بودن ناچ، نمایش بده (WHEN CLOSED, SHOW)",
            ["notch.closed.time.title"] = "ساعت",
            ["notch.closed.time.desc"] = "ساعت حالت استراحت که پس از پایان رویدادها نمایش داده می‌شود",
            ["notch.closed.clockstyle"] = "سبک ساعت",
            ["notch.closed.digital"] = "دیجیتال",
            ["notch.closed.analog"] = "آنالوگ",
            ["notch.closed.date.title"] = "تاریخ",
            ["notch.closed.date.desc"] = "نمایش روز و تاریخ در کنار ساعت (مثلاً Sat 3 · 9:41)",
            ["notch.closed.battery.title"] = "باتری",
            ["notch.closed.battery.desc"] = "نمایش درصد شارژ و اتصال به برق (در صورت غیرفعال بودن، ساعت در وسط ناچ قرار می‌گیرد)",
            ["notch.closed.battery.desc_pc"] = "روی کامپیوتر رومیزی (بدون باتری) غیرفعال است؛ ساعت خودکار در وسط قرار می‌گیرد",
            ["notch.closed.nowplaying.title"] = "در حال پخش (Now playing)",
            ["notch.closed.nowplaying.desc"] = "نمایش کنترل‌های فشرده رسانه هنگام پخش موسیقی یا ویدیو",
            ["notch.closed.bt.title"] = "بلوتوث",
            ["notch.closed.bt.desc"] = "نمایش آیکون هنگام اتصال هدفون یا دستگاه بلوتوثی",
            ["notch.sec.excluded"] = "برنامه‌های صوتی مستثنی‌شده",
            ["notch.excluded.title"] = "نادیده گرفتن صدای برنامه‌های خاص",
            ["notch.excluded.desc"] = "هنگام پخش صدا از برنامه‌های انتخاب‌شده، ناچ وضعیت پخش رسانه را نمایش نخواهد داد.",
            ["notch.excluded.select"] = "➕ انتخاب فایل .exe...",
            ["notch.excluded.quick"] = "افزودن سریع: ",
            ["notch.excluded.empty"] = "هیچ برنامه‌ای مستثنی نشده است. صدای تمام پخش‌کننده‌ها و مرورگرها در ناچ نمایش داده می‌شود.",
            ["notch.restore"] = "بازگردانی به پیش‌فرض",
            ["notch.restore_msg"] = "تنظیمات بخش ناچ به حالت پیش‌فرض بازگردانده شد.",
            ["settings.banner.fontsize.title"] = "اندازه قلم اعلان‌ها و بنر",
            ["settings.banner.fontsize.desc"] = "تنظیم اندازه فونت متون کپی، کات و نوتیفیکیشن‌ها در ناچ",
            ["settings.banner.fontsize.small"] = "کوچک",
            ["settings.banner.fontsize.medium"] = "متوسط",
            ["settings.banner.fontsize.large"] = "بزرگ",
            ["settings.banner.fontsize.xlarge"] = "خیلی بزرگ",
            ["settings.live.header.keyboard"] = "صفحه‌کلید و ورودی",
            ["settings.live.keyboard.title"] = "تغییر زبان کیبورد",
            ["settings.live.keyboard.desc"] = "نمایش بنر انیمیشنی ناچ هنگام تغییر زبان صفحه‌کلید (Alt+Shift و Win+Space)",
        };

        private static readonly Dictionary<string, string> _en = new(StringComparer.OrdinalIgnoreCase)
        {
            // General Page
            ["general.title"] = "General",
            ["general.subtitle"] = "Startup, shortcut, time and units",
            ["general.startup_title"] = "Start Mayli when Windows starts",
            ["general.startup_desc"] = "Starts silently each time you sign in. Quit applies only to this session. To have it back seconds after a restart, turn on \"Restart apps\" in Windows Settings > Accounts > Sign-in options.",
            ["general.shortcut_title"] = "Open the notch",
            ["general.shortcut_desc"] = "Opens or closes the notch from anywhere",
            ["general.change"] = "Change",
            ["general.press_keys"] = "Press keys...",
            ["general.time_title"] = "Time format",
            ["general.time_desc"] = "Used by the notch, Home and Clock",
            ["general.time.system"] = "System",
            ["general.time.12h"] = "12-hour",
            ["general.time.24h"] = "24-hour",
            ["general.temp_title"] = "Temperature",
            ["general.temp_desc"] = "Weather, Home and the notch",
            ["general.wind_title"] = "Wind speed",
            ["general.wind_desc"] = "Used by Weather and Home",
            ["general.lang_title"] = "Language",
            ["general.lang_desc"] = "Interface display language (English or Persian)",

            // Navigation
            ["nav.general"] = "General",
            ["nav.notch"] = "Notch",
            ["nav.appearance"] = "Appearance",
            ["nav.modules"] = "Modules",
            ["nav.live_activities"] = "Live Activities",
            ["nav.privacy"] = "Privacy",
            ["nav.updates"] = "Updates",
            ["nav.about"] = "About",
            ["nav.search"] = "Search",
            ["nav.subtitle"] = "The Notch for Windows",
            ["nav.help"] = "Help",
            ["nav.feedback"] = "Feedback",

            // Updates
            ["updates.title"] = "Updates",
            ["updates.subtitle"] = "Version information and update channels",
            ["updates.installed_title"] = "Installed version",
            ["updates.installed_desc"] = "Mayli v{0} (Windows x64 Architecture).",
            ["updates.up_to_date"] = "✓ Up to date (v{0})",
            ["updates.available"] = "🚀 Update available: v{0}",
            ["updates.check_title"] = "Check for updates",
            ["updates.check_desc"] = "Queries github.com/Mahdilvl20/Mayli for newly published releases and fixes.",
            ["updates.check_now"] = "Check Now",
            ["updates.checking"] = "Checking...",
            ["updates.on_latest"] = "You're on latest!",
            ["updates.download_btn"] = "Download v{0} ↗",
            ["updates.channel_title"] = "Release channel",
            ["updates.channel_desc"] = "Choose between thoroughly tested Stable releases or experimental Beta features.",
            ["updates.channel_stable"] = "Stable",
            ["updates.channel_beta"] = "Beta",

            // Tools Titles
            ["tool.home"] = "Home",
            ["tool.system"] = "System",
            ["tool.clipboard"] = "Clipboard",
            ["tool.notes"] = "Notes",
            ["tool.focus"] = "Timers",
            ["tool.fileshelf"] = "File Shelf",
            ["tool.weather"] = "Weather",
            ["tool.clock"] = "Clock",
            ["tool.calendar"] = "Calendar",
            ["dock.calendar"] = "Calendar",
            ["sub.calendar"] = "Solar & Gregorian · Holidays & Events",
            ["cal.shamsi"] = "Solar (Jalali)",
            ["cal.gregorian"] = "Gregorian",
            ["cal.today"] = "Today",
            ["cal.holiday"] = "Official Holiday",
            ["cal.working_day"] = "Working Day",
            ["cal.events_title"] = "Events & Holidays",
            ["cal.no_events"] = "No special events for this day",
            ["cal.copy_date"] = "Copy Date",
            ["cal.copied_hint"] = "Date copied",
            ["cal.source_api"] = "holidayapi.ir",
            ["cal.source_builtin"] = "Built-in Database",
            ["tool.maylidrop"] = "Mayli Drop",
            ["tool.units"] = "Units",
            ["tool.links"] = "Links",
            ["tool.message"] = "Message",
            ["tool.screentime"] = "Screen Time",
            ["tool.nowplaying"] = "Now Playing",
            ["tool.water"] = "Water",
            ["tool.stopwatch"] = "Stopwatch",

            // Modules Page
            ["modules.title"] = "Modules",
            ["modules.subtitle"] = "Choose what lives in your notch. Drag to reorder.",
            ["modules.dock.header"] = "DOCK",
            ["modules.sec.essentials"] = "ESSENTIALS",
            ["modules.sec.productivity"] = "PRODUCTIVITY",
            ["modules.sec.insights"] = "INSIGHTS",
            ["modules.sec.homewidgets"] = "HOME WIDGETS",
            ["modules.reset_order"] = "Reset order",
            ["modules.tip"] = "Tip: changes show in the dock right away.",
            ["modules.home.desc"] = "Now playing, your next event and quick stats",
            ["modules.system.desc"] = "CPU, memory, disk and battery",
            ["modules.focus.desc"] = "Pomodoro, countdown, stopwatch and water",
            ["modules.weather.desc"] = "Now, hourly and five-day forecast",
            ["modules.clock.desc"] = "World clocks, digital or analog",
            ["modules.calendar.desc"] = "Agenda, festivals and reminders",
            ["modules.clipboard.desc"] = "Everything you copied, searchable",
            ["modules.notes.desc"] = "Quick notes that stay with the notch",
            ["modules.fileshelf.desc"] = "Park files and drag them anywhere",
            ["modules.units.desc"] = "Length, weight, temperature and more",
            ["modules.links.desc"] = "Bookmarks and quick web searches",
            ["modules.message.desc"] = "Flash your own text on the notch",
            ["modules.screentime.desc"] = "Where your day went, app by app",

            // Tool Subtitles
            ["sub.system"] = "Power · Network · Audio",
            ["sub.clock"] = "Local Time & Date",
            ["sub.units"] = "Conversion",
            ["sub.fileshelf"] = "Parked files",
            ["sub.links"] = "Quick Bookmarks",
            ["sub.message"] = "Notch banner",
            ["sub.focus"] = "Focus & intervals",
            ["sub.clipboard_fmt"] = "{0} items",
            ["sub.notes_fmt"] = "{0} notes",

            // Dock Tooltips
            ["dock.home"] = "Home",
            ["dock.system"] = "System",
            ["dock.clipboard"] = "Clipboard",
            ["dock.notes"] = "Notes",
            ["dock.focus"] = "Timers",
            ["dock.fileshelf"] = "File Shelf",
            ["dock.weather"] = "Weather",
            ["dock.clock"] = "Clock",
            ["dock.units"] = "Units",
            ["dock.links"] = "Links",
            ["dock.message"] = "Message",
            ["dock.screentime"] = "Screen Time",
            ["dock.customize"] = "Add / Customize Tools",
            ["screentime.tab_7d"] = "7 Days",
            ["screentime.tab_30d"] = "30 Days",
            ["screentime.last30days"] = "Last 30 days",
            ["screentime.click_inspect"] = "Click any square to inspect",
            ["screentime.no_activity"] = "No activity",

            // Header Buttons
            ["header.close"] = "Close (Esc)",
            ["header.settings"] = "Settings",
            ["header.theme_format"] = "Theme: {0} (Click to switch)",
            ["header.orb"] = "Mayli Companion Orb 🐾 (Click to emote)",

            // Tray Menu
            ["tray.toggle"] = "Toggle Notch (Ctrl+Alt+N)",
            ["tray.settings"] = "Settings...",
            ["tray.mood"] = "Mayli Mood / Companion 🐾",
            ["tray.cycle_mood"] = "Cycle Mood",
            ["tray.exit"] = "Exit Mayli",

            // Home Tool
            ["home.screentime"] = "SCREEN TIME",
            ["home.system"] = "SYSTEM",
            ["home.weather"] = "WEATHER",
            ["home.focus"] = "FOCUS",
            ["home.timer"] = "TIMER",
            ["home.clipboard"] = "CLIPBOARD",
            ["home.notes"] = "NOTES",
            ["home.nowplaying"] = "NOW PLAYING",
            ["home.nomedia"] = "No Media Playing",
            ["home.no_apps"] = "No apps today",
            ["home.focusing"] = "Focusing",
            ["home.paused"] = "Paused",
            ["home.start_focus"] = "Start Focus",
            ["home.no_clips"] = "No clips yet",
            ["home.no_notes"] = "No notes yet",

            // System Tool
            ["system.battery"] = "Battery",
            ["system.ac_power"] = "AC Power",
            ["system.desktop_pc"] = "Desktop PC",
            ["system.connected"] = "Connected",
            ["system.network"] = "Network",
            ["system.performance"] = "Performance",
            ["system.volume"] = "Volume",
            ["system.health_fmt"] = "Health {0}%",
            ["system.fully_charged"] = "Fully charged",
            ["system.charging"] = "Charging",
            ["system.on_battery"] = "On battery",
            ["system.muted"] = "Muted",

            // Notes Tool
            ["notes.all_notes"] = "All notes",
            ["notes.no_notes"] = "No notes yet",
            ["notes.new_note"] = "New note",
            ["notes.search"] = "Search notes...",
            ["notes.empty_prompt"] = "Create a note to start writing",
            ["notes.words"] = "words",
            ["notes.edited_now"] = "Edited just now",

            // Clipboard Tool
            ["clipboard.today"] = "Today",
            ["clipboard.search"] = "Search clips...",
            ["clipboard.copy"] = "Copy",
            ["clipboard.copied"] = "Copied! ✓",
            ["clipboard.no_items"] = "No items found",
            ["clipboard.open_link"] = "Open Link",

            // Screen Time Tool
            ["screentime.today"] = "Today",
            ["screentime.title"] = "Screen Time",
            ["screentime.screen_time"] = "Screen time",
            ["screentime.apps_used"] = "Apps used",
            ["screentime.last7days"] = "Last 7 days",
            ["screentime.by_app"] = "By app",
            ["screentime.avg_fmt"] = "avg {0}",
            ["screentime.no_activity"] = "No app activity recorded yet today.",
            ["screentime.refresh_tooltip"] = "Refresh App Usage",

            // Weather Tool
            ["weather.hourly"] = "Hourly",
            ["weather.5day"] = "5-Day",
            ["weather.back"] = "Back",
            ["weather.select_city"] = "Select City",
            ["weather.search_city"] = "Search city (e.g. Tehran, London, New York...)",
            ["weather.popular"] = "POPULAR & RECENT CITIES",
            ["weather.no_cities"] = "No cities found. Try another search term.",
            ["weather.humidity"] = "Humidity",
            ["weather.wind"] = "Wind",
            ["weather.uv"] = "UV Index",
            ["weather.sunrise"] = "Sunrise",
            ["weather.sunset"] = "Sunset",
            ["weather.feels_like"] = "Feels like",
            ["weather.updated_fmt"] = "Updated {0}",

            // Focus / Timers Tool
            ["focus.focus"] = "Focus",
            ["focus.break"] = "Break",
            ["focus.short_break"] = "Short Break",
            ["focus.long_break"] = "Long Break",
            ["focus.auto_start"] = "Auto-start next",
            ["focus.completed_today_fmt"] = "{0} completed today",
            ["focus.focus_min_fmt"] = "Focus {0}m",
            ["focus.break_min_fmt"] = "Break {0}m",
            ["focus.long_min_fmt"] = "Long {0}m",
            ["focus.cycles_fmt"] = "Cycles {0}",
            ["focus.cycle_header_fmt"] = "{0} {1} of {2}",
            ["focus.next_break_fmt"] = "Break {0} next",

            // File Shelf Tool
            ["shelf.shelves"] = "SHELVES",
            ["shelf.new_shelf"] = "+ New shelf",
            ["shelf.empty"] = "Empty",
            ["shelf.items_fmt"] = "{0} items · {1}",
            ["shelf.choose_files"] = "Choose files",
            ["shelf.empty_desc"] = "Nothing on Desk yet — drop files or paste with Ctrl+V",

            // Clock Tool
            ["clock.clock"] = "Clock",
            ["clock.time_format"] = "Time format",
            ["clock.show_seconds"] = "Show seconds",
            ["clock.show_date"] = "Show date",
            ["clock.date_style"] = "Date style",
            ["clock.change"] = "Change",
            ["clock.world_clocks"] = "World clocks",
            ["clock.add_city"] = "+ Add city",
            ["clock.add_world_clock"] = "Add World Clock",

            // Units Tool
            ["units.from"] = "From",
            ["units.to"] = "To",
            ["units.recent"] = "Recent",

            // Links Tool
            ["links.search"] = "Search",
            ["links.add_link"] = "+ Add Link",
            ["links.saved_links"] = "Saved links",
            ["links.add_bookmark"] = "Add Bookmark",
            ["links.links_count_fmt"] = "{0} links",

            // Message Tool
            ["message.live_preview"] = "Live preview",
            ["message.placeholder"] = "Type a message",
            ["message.flash"] = "Flash",
            ["message.clear"] = "Clear",
            ["message.take_down"] = "Take Down",
            ["message.speed"] = "Speed",
            ["message.speed.slow"] = "Slow",
            ["message.speed.normal"] = "Normal",
            ["message.speed.fast"] = "Fast",
            ["message.repeat"] = "Repeat until taken down",
            ["message.animation"] = "Animation",
            ["message.anim.slide"] = "Slide",
            ["message.anim.fade"] = "Fade",
            ["message.anim.typeon"] = "Type-on",
            ["message.anim.scroll"] = "Scroll",
            ["message.anim.blur"] = "Blur",
            ["message.anim.pulse"] = "Pulse",
            ["message.color"] = "Colour",
            ["message.finish"] = "Finish",
            ["message.finish.solid"] = "Solid",
            ["message.finish.silver"] = "Silver",
            ["message.finish.ocean"] = "Ocean",
            ["message.finish.aurora"] = "Aurora",
            ["message.duration"] = "Duration",
            ["message.shows_fmt"] = "Shows for {0}s",
            ["message.pinned"] = "Pinned",
            ["message.custom"] = "Custom",
            ["message.custom_sec"] = "Seconds",

            // Notch Settings Page
            ["notch.title"] = "Notch",
            ["notch.subtitle"] = "Shape, behavior and live preview",
            ["notch.preview.resting"] = "Resting",
            ["notch.preview.hover"] = "Hover",
            ["notch.sec.visibility"] = "VISIBILITY",
            ["notch.vis.always.title"] = "Always",
            ["notch.vis.always.desc"] = "Always on screen. Fullscreen rules below still apply.",
            ["notch.vis.smart.title"] = "Smart",
            ["notch.vis.smart.desc"] = "Full on the desktop; tucked away over maximized apps.",
            ["notch.vis.minimal.title"] = "Minimal",
            ["notch.vis.minimal.desc"] = "Just a small arrow at the top. Hover to peek, open on hover or click.",
            ["notch.vis.hidden.title"] = "Hidden",
            ["notch.vis.hidden.desc"] = "Nothing on screen. Open it from the tray icon (or set a shortcut in General).",
            ["notch.sec.style"] = "STYLE",
            ["notch.style.line.title"] = "Line",
            ["notch.style.line.desc"] = "A slim bar along the top edge",
            ["notch.style.notch.title"] = "Notch",
            ["notch.style.notch.desc"] = "Grows out of the screen edge",
            ["notch.style.island.title"] = "Island",
            ["notch.style.island.desc"] = "A pill floating just below the edge",
            ["notch.shape.title"] = "Notch shape",
            ["notch.shape.desc"] = "Fine-tune the Notch style's silhouette",
            ["notch.size.title"] = "Size",
            ["notch.size.desc"] = "Scales the resting notch",
            ["notch.layout.title"] = "Resting layout",
            ["notch.layout.desc"] = "Time and battery in corners or center",
            ["notch.layout.corners"] = "Corners",
            ["notch.layout.center"] = "Center",
            ["notch.opens.title"] = "Opens on",
            ["notch.opens.desc"] = "Hover to open at once, or click to open on purpose",
            ["notch.opens.hover"] = "Hover",
            ["notch.opens.click"] = "Click",
            ["notch.display.title"] = "Display",
            ["notch.display.desc"] = "Which monitor the notch lives on",
            ["notch.shownotch.title"] = "Show notch",
            ["notch.shownotch.desc"] = "Hide it without quitting; the tray icon brings it back",
            ["notch.sec.behavior"] = "BEHAVIOR",
            ["notch.fullscreen.title"] = "Hide in fullscreen",
            ["notch.fullscreen.desc"] = "Apps, videos and games on the notch's display",
            ["notch.events.title"] = "Show events over apps",
            ["notch.events.desc"] = "When the notch is tucked away, grow it briefly for events like volume, timers and a new song",
            ["notch.tabanim.title"] = "Tab animations",
            ["notch.tabanim.desc"] = "Slide between tabs inside the open notch",
            ["notch.sec.pet"] = "MAYLI PET (BETA)",
            ["notch.pet.title"] = "Mayli Pet (Beta)",
            ["notch.pet.desc"] = "A small companion in your notch",
            ["notch.sec.edgelight"] = "EDGE LIGHT",
            ["notch.edgelight.title"] = "Edge light",
            ["notch.edgelight.desc"] = "Light that runs along the notch outline",
            ["notch.edge.comet.title"] = "Comet",
            ["notch.edge.comet.desc"] = "Light sweeps right-to-left, then left-to-right",
            ["notch.edge.twin.title"] = "Twin comets",
            ["notch.edge.twin.desc"] = "Two lights meet at the bottom, then return",
            ["notch.edge.breathe.title"] = "Breathe",
            ["notch.edge.breathe.desc"] = "The whole outline glows softly",
            ["notch.edge.color"] = "Color",
            ["notch.edge.custom"] = "● Custom...",
            ["notch.edge.sync_music"] = "🎵 Match song when playing",
            ["notch.edge.intensity"] = "Intensity",
            ["notch.edge.speed"] = "Speed",
            ["notch.edge.speed.slow"] = "Slow",
            ["notch.edge.speed.normal"] = "Normal",
            ["notch.edge.speed.fast"] = "Fast",
            ["notch.edge.when.title"] = "When",
            ["notch.edge.when.desc"] = "On events & hover plays the light for a few seconds, then it fades out",
            ["notch.edge.when.always"] = "Always",
            ["notch.edge.when.events"] = "On events & hover",
            ["notch.sec.closed"] = "WHEN CLOSED, SHOW",
            ["notch.closed.time.title"] = "Time",
            ["notch.closed.time.desc"] = "The resting clock that events return to",
            ["notch.closed.clockstyle"] = "Clock style",
            ["notch.closed.digital"] = "Digital",
            ["notch.closed.analog"] = "Analog",
            ["notch.closed.date.title"] = "Date",
            ["notch.closed.date.desc"] = "Day and date next to the time, e.g. Sat 3 · 9:41",
            ["notch.closed.battery.title"] = "Battery",
            ["notch.closed.battery.desc"] = "Battery level and charging status (when disabled, the clock moves to the center)",
            ["notch.closed.battery.desc_pc"] = "Not available on Desktop PC (Clock is automatically centered)",
            ["notch.closed.nowplaying.title"] = "Now playing",
            ["notch.closed.nowplaying.desc"] = "Compact media controls while music or video plays",
            ["notch.closed.bt.title"] = "Bluetooth",
            ["notch.closed.bt.desc"] = "While a device is connected",
            ["notch.sec.excluded"] = "EXCLUDED AUDIO APPS",
            ["notch.excluded.title"] = "Ignore audio from specific apps",
            ["notch.excluded.desc"] = "When audio plays from selected apps, the notch will not show now playing or media activities.",
            ["notch.excluded.select"] = "➕ Select .exe...",
            ["notch.excluded.quick"] = "Quick add: ",
            ["notch.excluded.empty"] = "No apps excluded. Audio and media from all players and browsers will show on the notch.",
            ["notch.restore"] = "Restore defaults",
            ["notch.restore_msg"] = "Notch settings have been restored to factory defaults.",
            ["settings.banner.fontsize.title"] = "Banner text size",
            ["settings.banner.fontsize.desc"] = "Font size for Copy, Cut and notifications inside the notch",
            ["settings.banner.fontsize.small"] = "Small",
            ["settings.banner.fontsize.medium"] = "Medium",
            ["settings.banner.fontsize.large"] = "Large",
            ["settings.banner.fontsize.xlarge"] = "Extra Large",
            ["settings.live.header.keyboard"] = "KEYBOARD & INPUT",
            ["settings.live.keyboard.title"] = "Keyboard layout switch",
            ["settings.live.keyboard.desc"] = "Show animated notch banner when switching input language (Alt+Shift, Win+Space)",
        };

        public static string T(string key, string fallback = null)
        {
            if (_en.TryGetValue(key, out string enVal))
            {
                return enVal;
            }
            return fallback ?? key;
        }

        public static void SetLanguage(string lang)
        {
            AppPreferences.Instance.Language = "en";
            ApplyApplicationFont();
            LanguageChanged?.Invoke();
        }

        public static void Initialize()
        {
            ApplyApplicationFont();
        }

        public static void ApplyApplicationFont()
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                var font = CurrentFontFamily;

                var textBlockStyle = new Style(typeof(System.Windows.Controls.TextBlock));
                textBlockStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontFamilyProperty, font));
                app.Resources[typeof(System.Windows.Controls.TextBlock)] = textBlockStyle;

                var controlStyle = new Style(typeof(System.Windows.Controls.Control));
                controlStyle.Setters.Add(new Setter(System.Windows.Controls.Control.FontFamilyProperty, font));
                app.Resources[typeof(System.Windows.Controls.Control)] = controlStyle;
            }
            catch { }
        }

        public static void ApplyToWindow(Window window)
        {
            if (window == null) return;
            window.FontFamily = CurrentFontFamily;
            window.FlowDirection = CurrentFlowDirection;
        }
    }
}
