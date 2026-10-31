# برنامه تسک‌بندی و پیاده‌سازی Vayli (`WindowsBuddy`) با Git

این پروژه بر اساس خروجی‌های استخراج‌شده توسط **[REA (`morluto/rea`)](file:///F:/rea_repo)** از باینری [Vayli.exe](file:///F:/Vayli_Reversed/Vayli.exe) (`658` کلاس اصلی، `211` تابع P/Invoke و `77` ریسورس توکار) به صورت ماژولار در ریپازیتوری گیت [F:/Vayli_Project](file:///F:/Vayli_Project) پیاده‌سازی و با **صفر خطا و صفر هشدار (`0 Warning(s), 0 Error(s)`)** بیلد شد.

---

## وضعیت تسک‌ها

- [x] **تسک ۰: تحلیل و استخراج متادیتا با REA (`morluto/rea`)**
  - [x] اجرای `inspect-desktop-application` روی پوشه برنامه
  - [x] اجرای `inspect-managed-artifact` و استخراج کامل ساختار PE/CLI و ۷۷ ریسورس
  - [x] اجرای `inspect-managed-members` و استخراج ۱,۹۱۱ تایپ و فضای نام‌ها (`WindowsBuddy.*`)
  - [x] اجرای `inspect-managed-native-boundaries` و استخراج ۲۱۱ تابع P/Invoke

- [x] **تسک ۱: راه‌اندازی ریپازیتوری Git، ساختار Solution و استخراج ریسورس‌های اصلی (`Assets & Fonts`)**
  - [x] ساخت ریپازیتوری گیت در [F:/Vayli_Project](file:///F:/Vayli_Project) و تنظیم [.gitignore](file:///F:/Vayli_Project/.gitignore)
  - [x] استخراج کامل ۷۷ ریسورس جاسازی‌شده در `Vayli.exe` در پوشه [Resources](file:///F:/Vayli_Project/src/Vayli/Resources) (فونت‌های `Inter` و `Geist`، اسپرایت‌های `Vayli.Pet`، مختصات `Vayli.Pet.eye-areas.json`، آیکون‌های `Brand` و `Emoji`)
  - [x] ایجاد ساختار پروژه WPF ([Vayli.sln](file:///F:/Vayli_Project/Vayli.sln) و [Vayli.csproj](file:///F:/Vayli_Project/src/Vayli/Vayli.csproj)) با فضای نام اصلی `WindowsBuddy`
  - [x] ثبت کامیت اول (`7b098b4`)

- [x] **تسک ۲: لایه هسته، مدیریت تنظیمات و توابع Native ویندوز (`WindowsBuddy.Core` & `Services`)**
  - [x] پیاده‌سازی P/Invokeهای ویندوز در [NativeMethods.cs](file:///F:/Vayli_Project/src/Vayli/Core/NativeMethods.cs) (`user32.dll`, `kernel32.dll`) بر اساس `native_boundaries.json`
  - [x] پیاده‌سازی [LogWriter.cs](file:///F:/Vayli_Project/src/Vayli/Core/LogWriter.cs)، `EmbeddedAssetLoader`، [AppPaths.cs](file:///F:/Vayli_Project/src/Vayli/Services/AppPaths.cs)، `PersistenceHelper`، [AppPreferences.cs](file:///F:/Vayli_Project/src/Vayli/Services/AppPreferences.cs) و `HotkeyService`
  - [x] ثبت کامیت دوم (`33dc2f6`)

- [x] **تسک ۳: سیستم تم، توکن‌های طراحی، هندسه ناچ و کیت رابط کاربری (`BuddyCapsule.Theme`, `UI`, `Renderer`)**
  - [x] پیاده‌سازی [ThemePalette.cs](file:///F:/Vayli_Project/src/Vayli/BuddyCapsule/Theme/ThemePalette.cs) و `CapsuleTheme`
  - [x] پیاده‌سازی کنترل‌های اختصاصی در [BuddyControls.cs](file:///F:/Vayli_Project/src/Vayli/BuddyCapsule/UI/BuddyControls.cs) (`BuddyCard`, `BuddyPillButton`, `StaticBeatBars`, `EdgeLightWpf`)
  - [x] پیاده‌سازی [NotchGeometryBuilder.cs](file:///F:/Vayli_Project/src/Vayli/BuddyCapsule/Renderer/NotchGeometryBuilder.cs) با حالت‌های `Shouldered`, `Island`, `Compact`, `Wide` و `Line`
  - [x] ثبت کامیت سوم (`05adb0f`)

- [x] **تسک ۴ و ۵: ماشین وضعیت ناچ، سرویس‌های سیستمی و حیوان خانگی تعاملی (`NotchStateMachine`, `SystemMonitor`, `PetView`)**
  - [x] پیاده‌سازی [NotchStateMachine.cs](file:///F:/Vayli_Project/src/Vayli/BuddyCapsule/Core/NotchStateMachine.cs) با مقادیر ثابت دقیق REA (`CloseGraceMs = 150`, `PollActiveMs = 40`, `PollRestingMs = 250`)
  - [x] پیاده‌سازی [SystemMonitorService.cs](file:///F:/Vayli_Project/src/Vayli/Services/SystemMonitorService.cs) و `MediaService`
  - [x] پیاده‌سازی موتور حیوان خانگی تعاملی ناچ در [PetView.cs](file:///F:/Vayli_Project/src/Vayli/BuddyCapsule/Renderer/PetView.cs) با ترکیب لایه‌های `Vayli.Pet.body.*` و `Vayli.Pet.props.*` و رندر زنده چشم‌های `#30D158` بر اساس `Vayli.Pet.eye-areas.json` با قابلیت پلک زدن و تعقیب ماوس
  - [x] ثبت کامیت چهارم (`2086c50`)

- [x] **تسک ۶: ابزارک‌های اصلی داخل ناچ (`WindowsBuddy.Tools.*` - بخش اول)**
  - [x] پیاده‌سازی `IBuddyTool`, `ToolRegistry` و `HomeTool` در [CoreTools.cs](file:///F:/Vayli_Project/src/Vayli/Tools/CoreTools.cs)
  - [x] پیاده‌سازی `FileShelfTool` (قفسه Drag & Drop فایل‌ها)، `ClipboardTool` (تاریخچه زنده کلیپ‌بورد)، `NotesTool` (دفترچه یادداشت خودکار) و `TimerClockTool` (تایمر فوکوس و ساعت جهانی)
  - [x] ثبت کامیت پنجم (`59fbb69`)

- [x] **تسک ۷: ابزارک‌های پیشرفته (`Weather`, `ScreenTime`, `AICoding`, `Drop`)**
  - [x] پیاده‌سازی `WeatherTool`، `ScreenTimeTool` (ردیاب زنده برنامه‌های در حال استفاده با `GetForegroundWindow`)، `AICodingTool` و `DropTool` در [AdvancedTools.cs](file:///F:/Vayli_Project/src/Vayli/Tools/AdvancedTools.cs)
  - [x] ثبت کامیت ششم (`395d702`)

- [x] **تسک ۸: پنجره اصلی ناچ (`CapsuleWindow`)، پنجره تنظیمات (`SettingsWindow`) و آیکون System Tray**
  - [x] پیاده‌سازی [CapsuleWindow.cs](file:///F:/Vayli_Project/src/Vayli/BuddyCapsule/Core/CapsuleWindow.cs) با ابعاد دقیق استخراج‌شده از REA (`228x35` در حالت بسته و `752x352` در حالت باز درون پاکت `800x388`)
  - [x] پیاده‌سازی [SettingsWindow.cs](file:///F:/Vayli_Project/src/Vayli/UI/Settings/SettingsWindow.cs) و نقطه ورود [Program.cs](file:///F:/Vayli_Project/src/Vayli/Program.cs) همراه با آیکون System Tray و میانبر سراسری `Ctrl+Alt+N`
  - [x] بیلد نهایی Release و ثبت کامیت پایانی

- [x] **تسک ۹: تطبیق کامل و ۱:۱ رابط کاربری با تصاویر رسمی وایلی (`vaylishot`)**
  - [x] پیاده‌سازی داشبورد ۶ کارته اصلی در [HomeTool](file:///F:/Vayli_Project/src/Vayli/Tools/CoreTools.cs) شامل کارت‌های `Screen Time` (با نمودار میله‌ای ۷ روز هفته)، `System` (با ۳ گیج حلقوی رادیال `CPU / RAM / DISK`)، `Weather` (دما، خورشید، دامنه `H/L`)، `Focus` (تایمر معکوس و دکمه دایره‌ای مکث)، `Clipboard` (اسنیپت‌های سریع) و `Notes` (چک‌لیست تسک‌ها)
  - [x] پیاده‌سازی ۴ تم رسمی در [ThemePalette.cs](file:///F:/Vayli_Project/src/Vayli/BuddyCapsule/Theme/ThemePalette.cs) شامل `Pure Black`، `Ember Dark`، `Vivid` و `Silver` با قابلیت تعویض سریع با یک کلیک از هدر ناچ
  - [x] ارتقای [WeatherTool](file:///F:/Vayli_Project/src/Vayli/Tools/AdvancedTools.cs) به ۳ ستون اختصاصی (هوای فعلی، پیش‌بینی ساعتی با نمودار خطی پیوسته و شاخص‌های محیطی، پیش‌بینی ۵ روزه با اسلایدرهای رِنج دما)
  - [x] پیاده‌سازی [FileShelfTool](file:///F:/Vayli_Project/src/Vayli/Tools/CoreTools.cs) (سایدبار قفسه‌ها، گرید فایل‌های پارک‌شده با آیکون‌های ZIP، Word، PDF و تصویر، باکس Drag&Drop) و [LinksTool](file:///F:/Vayli_Project/src/Vayli/Tools/CoreTools.cs) (جستجوی سریع، چیپ‌های سایت‌های پرکاربرد، کارت‌های بوک‌مارک)
  - [x] ارتقای داک ناوبری پایینی در [CapsuleWindow.cs](file:///F:/Vayli_Project/src/Vayli/BuddyCapsule/Core/CapsuleWindow.cs) با ۱۸ ابزار متناظر، دایره هایلایت و نقطه سفید زیر تب فعال
  - [x] پیاده‌سازی بنرهای نوتیفیکیشن داینامیک ناچ (`Copied · Image`, `Screenshot saved`, `AirPods Pro Connected`) و پیام‌های کاراکتر ربات وایلی (`Good morning, Alex!`)
  - [x] ثبت کامیت (`581a9cc`) و تایید بیلد کامل Release با ۰ خطا و ۰ هشدار

- [x] **تسک ۱۰: واقعی‌سازی کامل اکشن‌های ناچ ویندوز (Dynamic Island Real-Time Events)**
  - [x] پیاده‌سازی سرویس مرکزی رویدادهای زنده سیستم‌عامل در [RealtimeNotchActionService.cs](file:///F:/Vayli_Project/src/Vayli/Services/RealtimeNotchActionService.cs)
  - [x] تشخیص زنده پخش موزیک: بررسی سرویس‌های پخش زنده (Spotify، مرورگرها، YouTube و مدیا پلیر) و تغییر ناچ در حالت بسته به پلیر پویا (`290x35`) با نمایش نام ترک، خواننده و میله‌های ضرب‌آهنگ متحرک (`StaticBeatBars`)
  - [x] تشخیص زنده اتصال بلوتوث: لیسنر سخت‌افزاری `WM_DEVICECHANGE` (`0x0219`) و کوئری رجیستری برای تجهیزات هندزفری و بلوتوث و نمایش فوری بنر ناچ (`AirPods Pro / QCY-T13 Connected`) با ابعاد کپسول عریض (`340x38`)
  - [x] تشخیص زنده کپی متن و عکس: هوک سراسری `WM_CLIPBOARDUPDATE` و تفکیک تصاویر (`Copied · Image`)، متن‌ها (`Copied · "<متن>"`) و فایل‌ها به همراه مانیتورینگ پوشهٔ اسکرین‌شات‌ها (`Screenshot saved`)
  - [x] اتصال انیمیشن مورف پویا در [CapsuleWindow.cs](file:///F:/Vayli_Project/src/Vayli/BuddyCapsule/Core/CapsuleWindow.cs) با تنظیم بازگشت خودکار کپسول به ابعاد عادی پس از اتمام نوتیفیکیشن
  - [x] ثبت کامیت (`e70644e`) و تایید بیلد کامل Release با ۰ خطا و ۰ هشدار

- [x] **تسک ۱۱: بازطراحی دقیق بخش آهنگ ناچ مطابق با تصویر کاربر (Waveform Visualizer & Mini Equalizer)**
  - [x] پیاده‌سازی کنترل کاور آرت اختصاصی در [BuddyControls.AlbumArtThumbnail](file:///F:/Vayli_Project/src/Vayli/BuddyCapsule/UI/BuddyControls.cs) با آیکون و کادر گوشه‌گرد `24x24`
  - [x] پیاده‌سازی آیکون اکولایزر پویا و متحرک در [BuddyControls.MiniEqualizerControl](file:///F:/Vayli_Project/src/Vayli/BuddyCapsule/UI/BuddyControls.cs) با ساختار دقیق `I · II` همراه با قابلیت کلیک Play/Pause
  - [x] پیاده‌سازی اسکرابر و اکولایزر موج صوتی پیوسته در [BuddyControls.WaveformProgressBar](file:///F:/Vayli_Project/src/Vayli/BuddyCapsule/UI/BuddyControls.cs) با ۴۲ ستون موج صوتی (Waveform) با تفکیک بخش پخش‌شده سفید و باقی‌مانده محو
  - [x] تنظیم ابعاد اختصاصی کپسول آهنگ در [CapsuleWindow.cs](file:///F:/Vayli_Project/src/Vayli/BuddyCapsule/Core/CapsuleWindow.cs) به `336x46` و هاله نور ملایم EdgeLight دور ناچ
  - [x] ثبت کامیت (`70db54e`) و تایید بیلد کامل Release با ۰ خطا و ۰ هشدار
