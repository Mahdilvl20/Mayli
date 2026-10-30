# برنامه تسک‌بندی و پیاده‌سازی Vayli (`WindowsBuddy`) با Git

این نقشه راه بر اساس خروجی‌های استخراج‌شده توسط **[REA (`morluto/rea`)](file:///F:/rea_repo)** از باینری [Vayli.exe](file:///F:/Vayli_Reversed/Vayli.exe) (`658` کلاس اصلی، `211` تابع P/Invoke و `77` ریسورس توکار) طراحی شده است. هر تسک در ریپازیتوری گیت [F:/Vayli_Project](file:///F:/Vayli_Project) دارای کامیت مجزا و استاندارد است.

---

## وضعیت تسک‌ها

- [x] **تسک ۰: تحلیل و استخراج متادیتا با REA (`morluto/rea`)**
  - [x] اجرای `inspect-desktop-application` روی پوشه برنامه
  - [x] اجرای `inspect-managed-artifact` و استخراج کامل ساختار PE/CLI و ۷۷ ریسورس
  - [x] اجرای `inspect-managed-members` و استخراج ۱,۹۱۱ تایپ و فضای نام‌ها (`WindowsBuddy.*`)
  - [x] اجرای `inspect-managed-native-boundaries` و استخراج ۲۱۱ تابع P/Invoke

- [/] **تسک ۱: راه‌اندازی ریپازیتوری Git، ساختار Solution و استخراج ریسورس‌های اصلی (`Assets & Fonts`)**
  - [ ] ساخت ریپازیتوری گیت در `F:\Vayli_Project` و تنظیم `.gitignore`
  - [ ] استخراج کامل ۷۷ ریسورس جاسازی‌شده در `Vayli.exe` (فونت‌های `Inter` و `Geist`، اسپرایت‌های `Vayli.Pet`، آیکون‌های `Brand` و `Emoji`)
  - [ ] ایجاد ساختار پروژه WPF (`Vayli.sln` و `src/Vayli/Vayli.csproj`) با فضای نام اصلی `WindowsBuddy`
  - [ ] ثبت کامیت اول (`chore: initialize Vayli project structure and extract embedded assets`)

- [ ] **تسک ۲: لایه هسته، مدیریت تنظیمات و توابع Native ویندوز (`WindowsBuddy.Core` & `Services`)**
  - [ ] پیاده‌سازی P/Invokeهای ویندوز (`user32.dll`, `dwmapi.dll`, `shell32.dll`, `kernel32.dll`) بر اساس `native_boundaries.json`
  - [ ] پیاده‌سازی `AppPaths`, `AppPreferences`, `PersistenceHelper`, `HotkeyService` و `LogWriter`
  - [ ] ثبت کامیت دوم (`feat(core): implement Win32 P/Invoke boundaries and core persistence services`)

- [ ] **تسک ۳: سیستم تم، توکن‌های طراحی و کیت رابط کاربری (`BuddyCapsule.Theme` & `BuddyCapsule.UI`)**
  - [ ] پیاده‌سازی `CapsuleTheme`, `ThemePalette`, `ThemeTokens`, `NotchConfig` و `BuddyTokens`
  - [ ] پیاده‌سازی کنترل‌های اختصاصی (`BuddyButton`, `BuddyIconButton`, `BuddyPillButton`, `BuddyCard`, `WidgetCard`, `EdgeLightWpf`)
  - [ ] ثبت کامیت سوم (`feat(ui): implement BuddyCapsule theme tokens, EdgeLight, and custom UI controls`)

- [ ] **تسک ۴: موتور اصلی ناچ و پنجره شناور بالای صفحه (`BuddyCapsule.Core`, `State`, `Behavior`, `Activity`)**
  - [ ] پیاده‌سازی ماشین وضعیت ناچ (`NotchPhase`, `NotchStateMachine`, `CapsuleStateManager`, `NotchVisibilityEngine`)
  - [ ] پیاده‌سازی هندسه ناچ (`NotchGeometryBuilder`) و انیمیشن‌های باز و بسته شدن (`CapsuleMotion`)
  - [ ] پیاده‌سازی پنجره اصلی `CapsuleWindow` (پنجره شفاف `Topmost` در بالای صفحه با قابلیت Hover، کلپس و اکسپند نرم) و `ToolRouter`
  - [ ] ثبت کامیت چهارم (`feat(capsule): implement CapsuleWindow, NotchStateMachine, and smooth notch geometry`)

- [ ] **تسک ۵: سرویس‌های سیستمی، مدیا، صدا و حیوان خانگی ناچ (`CoreAudio`, `Media`, `SystemMonitor`, `Pet`)**
  - [ ] پیاده‌سازی `CoreAudioService`، اکولایزر `StaticBeatBars` و `MediaService` / `MediaControlsView`
  - [ ] پیاده‌سازی `SystemMonitorService` (پایش CPU، رم و باتری)
  - [ ] پیاده‌سازی موتور حیوان خانگی تعاملی روی ناچ (`PetEngine`, `PetView`) با استفاده از اسپرایت‌های استخراج‌شده `Vayli.Pet.*`
  - [ ] ثبت کامیت پنجم (`feat(services): add media controls, audio beat bars, system telemetry, and interactive notch pet`)

- [ ] **تسک ۶: ابزارک‌های کاربردی داخل ناچ (`WindowsBuddy.Tools.*` - بخش اول)**
  - [ ] پیاده‌سازی `IBuddyTool`, `ToolRegistry`, `HomeTool`
  - [ ] پیاده‌سازی `ClockTool`، `TimerTool` (`CountdownEngine`) و `NotesTool`
  - [ ] پیاده‌سازی `ClipboardTool` (شنود زنده کلیپ‌بورد ویندوز) و `FileShelfTool` (قفسه Drag & Drop فایل‌ها)
  - [ ] ثبت کامیت ششم (`feat(tools): implement Home, Clock, Timer, Notes, Clipboard, and FileShelf notch tools`)

- [ ] **تسک ۷: ابزارک‌های پیشرفته (`Weather`, `Calendar`, `ScreenTime`, `AICoding`, `Drop`)**
  - [ ] پیاده‌سازی `WeatherTool` و `CalendarTool`
  - [ ] پیاده‌سازی `ScreenTimeTool` (پایش زمان استفاده از برنامه‌های فعال با `GetForegroundWindow`)
  - [ ] پیاده‌سازی `AICodingTool` و `DropTool` (اشتراک‌گذاری فایل در شبکه محلی با QR Code)
  - [ ] ثبت کامیت هفتم (`feat(tools): add Weather, Calendar, ScreenTime, AICoding, and LAN Drop tools`)

- [ ] **تسک ۸: پنجره تنظیمات، شخصی‌سازی تسک‌بار و آنبوردینگ (`UI.Settings`, `Taskbar`, `Onboarding`)**
  - [ ] پیاده‌سازی `SettingsWindow` با پیش‌نمایش زنده ناچ (`NotchPreview`) و انتخاب رنگ/سبک نور لبه (`EdgeSwatches`)
  - [ ] پیاده‌سازی `TaskbarService` و `OnboardingWindow` + آیکون System Tray
  - [ ] ثبت کامیت هشتم (`feat(settings): implement SettingsWindow, Taskbar personalization, and Tray integration`)
