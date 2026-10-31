using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WindowsBuddy.Tools.Units
{
    public static class UnitCatalog
    {
        public static readonly List<UnitCategory> All = new()
        {
            // 1. Length
            new UnitCategory
            {
                Name = "Length",
                Icon = "📏",
                DefaultFrom = "m",
                DefaultTo = "ft",
                Common = new[] { "in", "cm", "yd" },
                Units = new List<Unit>
                {
                    new Unit("mm", "Millimeters", 0.001m),
                    new Unit("cm", "Centimeters", 0.01m),
                    new Unit("m", "Meters", 1m),
                    new Unit("km", "Kilometers", 1000m),
                    new Unit("in", "Inches", 0.0254m),
                    new Unit("ft", "Feet", 0.3048m),
                    new Unit("yd", "Yards", 0.9144m),
                    new Unit("mi", "Miles", 1609.344m),
                    new Unit("nmi", "Nautical miles", 1852m),
                    new Unit("µm", "Micrometers", 0.000001m)
                }
            },

            // 2. Mass
            new UnitCategory
            {
                Name = "Mass",
                Icon = "⚖",
                DefaultFrom = "kg",
                DefaultTo = "lb",
                Common = new[] { "g", "oz", "st" },
                Units = new List<Unit>
                {
                    new Unit("mg", "Milligrams", 0.001m),
                    new Unit("g", "Grams", 1m),
                    new Unit("kg", "Kilograms", 1000m),
                    new Unit("t", "Tonnes", 1000000m),
                    new Unit("oz", "Ounces", 28.349523125m),
                    new Unit("lb", "Pounds", 453.59237m),
                    new Unit("st", "Stones", 6350.29318m)
                }
            },

            // 3. Temperature
            new UnitCategory
            {
                Name = "Temperature",
                Icon = "🌡",
                DefaultFrom = "°C",
                DefaultTo = "°F",
                Common = new[] { "K", "°F", "°C" },
                Units = new List<Unit>
                {
                    new Unit("°C", "Celsius", 1m),
                    new Unit("°F", "Fahrenheit", 1m),
                    new Unit("K", "Kelvin", 1m)
                }
            },

            // 4. Data
            new UnitCategory
            {
                Name = "Data",
                Icon = "🗄",
                DefaultFrom = "GB",
                DefaultTo = "MB",
                Common = new[] { "KB", "TB", "Mb" },
                Units = new List<Unit>
                {
                    new Unit("B", "Bytes", 1m),
                    new Unit("KB", "Kilobytes", 1024m),
                    new Unit("MB", "Megabytes", 1048576m),
                    new Unit("GB", "Gigabytes", 1073741824m),
                    new Unit("TB", "Terabytes", 1099511627776m),
                    new Unit("bit", "Bits", 0.125m),
                    new Unit("Kb", "Kilobits", 128m),
                    new Unit("Mb", "Megabits", 131072m),
                    new Unit("Gb", "Gigabits", 134217728m)
                }
            },

            // 5. Volume
            new UnitCategory
            {
                Name = "Volume",
                Icon = "🥛",
                DefaultFrom = "l",
                DefaultTo = "gal",
                Common = new[] { "ml", "cup", "fl oz" },
                Units = new List<Unit>
                {
                    new Unit("ml", "Milliliters", 1m),
                    new Unit("l", "Liters", 1000m),
                    new Unit("m³", "Cubic meters", 1000000m),
                    new Unit("tsp", "Teaspoons", 4.92892159375m),
                    new Unit("tbsp", "Tablespoons", 14.78676478125m),
                    new Unit("cup", "Cups", 236.5882365m),
                    new Unit("fl oz", "Fluid ounces", 29.5735295625m),
                    new Unit("gal", "Gallons (US)", 3785.411784m)
                }
            },

            // 6. Speed
            new UnitCategory
            {
                Name = "Speed",
                Icon = "🧭",
                DefaultFrom = "km/h",
                DefaultTo = "mph",
                Common = new[] { "m/s", "kn", "ft/s" },
                Units = new List<Unit>
                {
                    new Unit("m/s", "Meters per second", 1m),
                    new Unit("km/h", "Kilometers per hour", 0.2777777777777778m),
                    new Unit("mph", "Miles per hour", 0.44704m),
                    new Unit("kn", "Knots", 0.5144444444444444m),
                    new Unit("ft/s", "Feet per second", 0.3048m)
                }
            },

            // 7. Time
            new UnitCategory
            {
                Name = "Time",
                Icon = "🕒",
                DefaultFrom = "h",
                DefaultTo = "min",
                Common = new[] { "s", "day", "week" },
                Units = new List<Unit>
                {
                    new Unit("ms", "Milliseconds", 0.001m),
                    new Unit("s", "Seconds", 1m),
                    new Unit("min", "Minutes", 60m),
                    new Unit("h", "Hours", 3600m),
                    new Unit("day", "Days", 86400m),
                    new Unit("week", "Weeks", 604800m),
                    new Unit("month", "Months", 2630016m),
                    new Unit("year", "Years", 31557600m)
                }
            },

            // 8. Currency
            new UnitCategory
            {
                Name = "Currency",
                Icon = "💵",
                DefaultFrom = "USD",
                DefaultTo = "EUR",
                Common = new[] { "GBP", "JPY", "AED" },
                Units = new List<Unit>
                {
                    new Unit("USD", "US Dollar", 1m),
                    new Unit("EUR", "Euro", 0.92m),
                    new Unit("GBP", "British Pound", 0.77m),
                    new Unit("JPY", "Japanese Yen", 152.4m),
                    new Unit("AED", "UAE Dirham", 3.6725m),
                    new Unit("CAD", "Canadian Dollar", 1.39m),
                    new Unit("AUD", "Australian Dollar", 1.52m),
                    new Unit("CHF", "Swiss Franc", 0.86m),
                    new Unit("CNY", "Chinese Yuan", 7.12m),
                    new Unit("INR", "Indian Rupee", 84.08m)
                }
            }
        };

        public static UnitCategory FindCategory(string name)
        {
            return All.Find(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) ?? All[0];
        }

        public static decimal Convert(UnitCategory category, Unit from, Unit to, decimal value)
        {
            if (from == null || to == null) return value;
            if (from.Symbol == to.Symbol) return value;

            // Temperature special conversion
            if (category.IsTemperature)
            {
                return ConvertTemperature(from.Symbol, to.Symbol, value);
            }

            // Currency special conversion
            if (category.IsCurrency)
            {
                // from USD base: val / from.Factor * to.Factor
                if (from.Factor == 0) return 0;
                return value / from.Factor * to.Factor;
            }

            // Standard linear conversion
            if (to.Factor == 0) return 0;
            return (value * from.Factor) / to.Factor;
        }

        private static decimal ConvertTemperature(string from, string to, decimal value)
        {
            // Normalize to Celsius first
            decimal celsius = from switch
            {
                "°F" => (value - 32m) * 5m / 9m,
                "K" => value - 273.15m,
                _ => value
            };

            // Convert Celsius to Target
            return to switch
            {
                "°F" => (celsius * 9m / 5m) + 32m,
                "K" => celsius + 273.15m,
                _ => celsius
            };
        }

        public static string FormatResult(decimal value)
        {
            if (value == 0m) return "0";

            // Format with up to 5 significant decimal places, trim trailing zeroes
            string s = value.ToString("G6", CultureInfo.InvariantCulture);
            if (s.Contains('.') && !s.Contains('e') && !s.Contains('E'))
            {
                s = s.TrimEnd('0').TrimEnd('.');
            }
            return s;
        }
    }
}
