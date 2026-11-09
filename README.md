<div align="center">

<img src="src/Mayli/Resources/Vayli.Brand.mark-512.png" width="128" height="128" alt="Mayli Logo" />

# Mayli
### The Ultimate Dynamic Notch & Dynamic Island Experience for Windows 10 & 11
**ناچ هوشمند و جزیره پویا برای ویندوز**

[![GitHub Release](https://img.shields.io/github/v/release/Mahdilvl20/Mayli?style=for-the-badge&color=0071E3&logo=github)](https://github.com/Mahdilvl20/Mayli/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?style=for-the-badge&logo=windows)](https://github.com/Mahdilvl20/Mayli)
[![Architecture](https://img.shields.io/badge/Architecture-x64-555555?style=for-the-badge)](https://github.com/Mahdilvl20/Mayli)
[![Framework](https://img.shields.io/badge/Built%20with-.NET%2010%20%7C%20WPF%20%7C%20C%23-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

<br/>

**[Download Latest Installer (v1.1.64)](https://github.com/Mahdilvl20/Mayli/releases/latest)** • **[Features](#features)** • **[Shortcuts](#essential-shortcuts--interactions-)** • **[راهنمای فارسی](#-معرفی-مایلی-mayli---ناچ-هوشمند-ویندوز)**

---

</div>

## Overview | نمای کلی

**Mayli** brings the fluid, hardware-integrated aesthetics of the **MacBook Notch** and iPhone **Dynamic Island** straight to your Windows desktop. Designed from the ground up in high-performance C# / WPF on .NET 10, Mayli transforms the unused top bezel of your monitor into an intelligent, animated **Windows Notch** and **Dynamic Island**.

From real-time media player tracking with live audio spectrum visualizers to instant clipboard interactions, hardware HUD notifications, animated keyboard language switching, and split satellite islands—Mayli delivers the most refined **Notch for Windows** experience with zero system bloat and smooth 60/120 FPS hardware-accelerated animations.

---

## Key Highlights & Features

### 🖥️ Dynamic Notch Styles & Shapes
- **Notch Mode:** Seamlessly pinned to the top screen bezel, giving any monitor an authentic, elegant MacBook Pro notch feel.
- **Island Mode:** A floating, detached **Dynamic Island** that hovers gracefully at the top of your display.
- **Line Mode:** A ultra-thin, sleek status bar for minimalist setups.
- **Multiple Shapes & Sizes:** Choose between **Compact**, **Wide**, **Classic**, or **Ultra-Compact** notch profiles with dynamic auto-scaling (60% to 140%).

### 🎵 Live Activities & Media Notch
- **Universal Media Player Integration:** Deep GSMTC integration with Spotify, Apple Music, YouTube Music, Chrome, Edge, Firefox, VLC, and local media players.
- **Adaptive Dominant Color Palette:** The notch organically extracts dominant accent colors directly from album artwork.
- **Audio Spectrum Equalizer:** Real-time animated audio visualizer synced with sound playback.
- **Progress & Scrubbing:** Fluid progress bar with elapsed/remaining duration and instant play/pause/skip transport controls.

### 🌐 Animated Keyboard Language Switcher *(New in v1.1.64)*
- **Real-Time Layout Detection:** Instantly detects input language switches via `Alt + Shift`, `Win + Space`, `Ctrl + Shift`, or Windows taskbar selection.
- **High-Priority Notch Banner:** Language switch banners take first priority, seamlessly displaying language name and ISO badge (`فارسی · FA`, `English · EN`, `Türkçe · TR`, etc.).
- **3D Globe Vector Animation:** Integrated vector spinning globe animation derived from the *AnimateIcons* library.

### 🏝️ Split Satellite Islands
- When media is playing and a notification (or timer) arrives, Mayli dynamically splits into multi-island capsules:
  - **Left Island (Satellite):** Detaches the album artwork with animated beat glow.
  - **Main Capsule:** Expands to show the notification banner or language alert.
  - **Right Island:** Displays active countdown timers or battery charging indicators.

### 📋 Smart Clipboard & File Shelf
- **Animated Copy & Cut Banners:** Smooth vector animations (scissors for Cut, checkmark card for Copy) with live text or image previews.
- **Persistent Drag & Drop File Shelf:** Drag files, images, or snippets directly into the notch for temporary holding and quick sharing across apps.

### 📊 Expandable Dashboard & Widgets
Hover or click the notch to expand into a unified, glassmorphism dashboard:
- **System Monitor:** Real-time CPU, RAM, and Disk utilization gauges.
- **Screen Time:** Daily computer usage and top application tracker.
- **Weather Widget:** Live temperature, condition icons, and forecast.
- **Dual Calendar System:** Fully supports both **Jalali (هجری شمسی)** and **Gregorian** calendars with Persian day/month formatting.
- **Focus & Timers:** Pomodoro, custom countdowns, and stopwatch with detached notch tracking.
- **Quick Notes & Links:** Instant scratchpad and web shortcuts directly from the notch.

### 🎨 Themes & Customization
- **4 Official Aesthetic Themes:** *Pure Black*, *Ember Dark*, *Vivid*, and *Silver (Light Mode)*.
- **Customizable Banner Font Size:** Small, Medium, Large, and Extra Large font options.
- **Full Bilingual Localization:** Seamless switching between **English** and **فارسی (Persian)**.

---

## Essential Shortcuts & Interactions | جدول کلیدهای میانبر

| Shortcut / Interaction | کلید میانبر / روش تعامل | Action Description | توضیحات عملکرد |
| :--- | :--- | :--- | :--- |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>N</kbd> | <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>N</kbd> | **Toggle Notch Dashboard** | باز کردن / بستن داشبورد کامل ناچ |
| <kbd>Alt</kbd> + <kbd>Shift</kbd> | <kbd>Alt</kbd> + <kbd>Shift</kbd> | **Switch Language (Notch Banner)** | تغییر زبان کیبورد با بنر انیمیشنی کره زمین |
| <kbd>Win</kbd> + <kbd>Space</kbd> | <kbd>Win</kbd> + <kbd>Space</kbd> | **Cycle Input Layout** | پیمایش زبان‌های ورودی با نمایش فوری در ناچ |
| <kbd>Ctrl</kbd> + <kbd>C</kbd> | <kbd>Ctrl</kbd> + <kbd>C</kbd> | **Copy Notification** | نمایش بنر کپی با انیمیشن کارت و تیک |
| <kbd>Ctrl</kbd> + <kbd>X</kbd> / <kbd>Shift</kbd> + <kbd>Del</kbd> | <kbd>Ctrl</kbd> + <kbd>X</kbd> | **Cut Notification** | نمایش بنر برش با انیمیشن قیچی |
| <kbd>Win</kbd> + <kbd>Shift</kbd> + <kbd>S</kbd> | <kbd>Win</kbd> + <kbd>Shift</kbd> + <kbd>S</kbd> | **Screenshot Detection** | اسکرین‌شات Snipping Tool با پیش‌نمایش در ناچ |
| **Hover on Notch** | **نگه داشتن موس روی ناچ** | **Quick Peek / Clock** | پیش‌نمایش سریع ساعت، وضعیت موزیک یا تایمر |
| **Click on Notch** | **کلیک چپ روی ناچ** | **Expand Dashboard** | باز کردن کارت‌های ابزار و داشبورد گسترده |
| **Right-Click on Notch** | **کلیک راست روی ناچ** | **Notch Quick Menu / Settings** | دسترسی سریع به تنظیمات و منوی زمینه |
| **Drag & Drop to Notch** | **کشیدن و رها کردن فایل** | **Drop into File Shelf** | نگهداری موقت فایل‌ها و عکس‌ها در قفسه ناچ |
| **Notch Media Controls** | **دکمه‌های روی ناچ** | **Play / Pause / Next / Prev** | کنترل پخش موسیقی و ویدیو بدون ترک برنامه فعال |

*(Note: The global dashboard shortcut `Ctrl + Alt + N` can be customized in Mayli Settings).*

---

## 📥 Installation

### Method 1: Windows Setup Installer (Recommended)
1. Download the latest installer **[Mayli_Setup_v1.1.64.exe](https://github.com/Mahdilvl20/Mayli/releases/latest)** from GitHub Releases.
2. Run the installer and follow the quick setup wizard.
3. Mayli will launch automatically and anchor to the top of your screen.

### Method 2: Build From Source
```powershell
# Clone the repository
git clone https://github.com/Mahdilvl20/Mayli.git
cd Mayli

# Build in Release mode
dotnet build src/Mayli/Mayli.csproj -c Release

# Run Mayli
./src/Mayli/bin/Release/net10.0-windows10.0.19041.0/Mayli.exe
```

---

<div dir="rtl" align="right">

## 🇮🇷 معرفی مایلی (Mayli) - ناچ هوشمند ویندوز

**مایلی (Mayli)** نرم‌افزاری مدرن، سریع و سبک است که تجربه جذاب و کاربردی **ناچ مک‌بوک (MacBook Notch)** و **داینامیک آیلند (Dynamic Island)** را با بالاترین کیفیت بصری به ویندوز ۱۰ و ۱۱ می‌آورد. این برنامه بدون افت فریم و با استفاده حداقلی از منابع سیستم، لبه بالایی مانیتور شما را به یک مرکز کنترل هوشمند و زنده تبدیل می‌کند.

### قابلیت‌های برجسته مایلی:
1. **استایل‌های متنوع ناچ:** امکان انتخاب میان حالت **ناچ متصل به لبه (Notch)**، **جزیره شناور (Dynamic Island)** و **خط مینیمال (Line)** با سایزهای کامپکت، عریض و کلاسیک.
2. **پخش زنده رسانه و آهنگ (Now Playing):** پشتیبانی از اسپاتیفای، اپل موزیک، یوتیوب، ساندکلاد و تمام مرورگرها همراه با اکولایزر زنده، استخراج رنگ کاور آرت و دکمه‌های کنترل آهنگ.
3. **انیمیشن تغییر زبان کیبورد (جدید در نسخه ۱.۱.۶۴):** نمایش بنر انیمیشنی کره زمین با اولویت بالا هنگام فشردن `Alt + Shift` یا `Win + Space` با تشخیص آنی و نمایش عنوان زبان (`فارسی · FA` و `English · EN`).
4. **جزیره‌های ماهواره‌ای جداشونده (Split Satellite Islands):** هنگام پخش آهنگ و ورود اعلان یا تایمر، کاور آهنگ به صورت خودکار به جزیره سمت چپ و ارقام تایمر به جزیره سمت راست منتقل می‌شوند.
5. **بنر کپی و کات اختصاصی:** انیمیشن‌های روان وکتوری برای کپی و کات متن، تصویر و فایل‌ها با امکان تنظیم سایز فونت بنر در تنظیمات.
6. **داشبورد ابزارها:** دسترسی آسان با کلیک یا کلید میانبر به مانیتور سیستم (CPU/RAM/Disk)، زمان مصرف صفحه (Screen Time)، وضعیت آب و هوا، تایمر و پومودورو، تقویم دوزبانه شمسی و میلادی، یادداشت‌ها و قفسه فایل موقت.
7. **پشتیبانی کامل از زبان فارسی:** رابط کاربری کاملاً دوزبانه (فارسی و انگلیسی) همراه با تقویم هجری شمسی و تم‌های دارک و لایت.

---

</div>

## Tech Stack & Architecture

- **Language & Runtime:** C# 13, .NET 10 (`net10.0-windows10.0.19041.0`)
- **UI Framework:** Windows Presentation Foundation (WPF) with pure hardware-accelerated Direct3D composition
- **Windows APIs & Hooks:** Win32 Low-Level Keyboard Hooks (`WH_KEYBOARD_LL`), WinEvent hooks, GSMTC (`Windows.Media.Control`), CoreAudio WASAPI, WMI brightness & monitor DDC/CI
- **Packaging:** Inno Setup 6 (Ultra LZMA2 compressed standalone installer)

---

## License

This project is licensed under the [MIT License](LICENSE).

<div align="center">

Crafted with care by **[Mahdi](https://github.com/Mahdilvl20)** & Modi Studio.  
*Give a ⭐ on GitHub if you love having a Dynamic Notch on Windows!*

</div>
