using System;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services
{
    public static class DeviceService
    {
        private static bool? _isLaptop;

        /// <summary>
        /// Returns true if the device is a portable/battery-powered device (Laptop, Notebook, Tablet).
        /// Returns false for Desktop PCs, Workstations, Servers.
        /// </summary>
        public static bool IsLaptop
        {
            get
            {
                if (_isLaptop.HasValue) return _isLaptop.Value;
                _isLaptop = DetectIsLaptop();
                return _isLaptop.Value;
            }
        }

        /// <summary>
        /// Returns true if the system has an active physical battery installed.
        /// On Desktop PCs without battery or UPS, this returns false.
        /// </summary>
        public static bool HasBattery
        {
            get
            {
                try
                {
                    if (NativeMethods.GetSystemPowerStatus(out var sps))
                    {
                        // sps.BatteryFlag:
                        // 128 (0x80) = No system battery
                        // 255 (0xFF) = Unknown status
                        // sps.BatteryLifePercent: 255 = Unknown status
                        bool noBattery = (sps.BatteryFlag & 128) != 0 || sps.BatteryFlag == 255 || sps.BatteryLifePercent > 100;
                        return !noBattery;
                    }
                }
                catch { }

                return false;
            }
        }

        /// <summary>
        /// Determines if the device should display battery indicators.
        /// Desktop computers without battery return false.
        /// </summary>
        public static bool ShouldShowBatteryIndicators => HasBattery;

        private static bool DetectIsLaptop()
        {
            try
            {
                var role = NativeMethods.PowerDeterminePlatformRole();
                if (role == NativeMethods.POWER_PLATFORM_ROLE.PlatformRoleMobile ||
                    role == NativeMethods.POWER_PLATFORM_ROLE.PlatformRoleSlate)
                {
                    return true;
                }
                if (role == NativeMethods.POWER_PLATFORM_ROLE.PlatformRoleDesktop ||
                    role == NativeMethods.POWER_PLATFORM_ROLE.PlatformRoleWorkstation ||
                    role == NativeMethods.POWER_PLATFORM_ROLE.PlatformRoleEnterpriseServer ||
                    role == NativeMethods.POWER_PLATFORM_ROLE.PlatformRoleSOHOServer)
                {
                    return false;
                }
            }
            catch { }

            return HasBattery;
        }
    }
}
