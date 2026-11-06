using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Units
{
    public sealed class Unit
    {
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Factor { get; set; } = 1m;

        public Unit() { }

        public Unit(string symbol, string name, decimal factor)
        {
            Symbol = symbol;
            Name = name;
            Factor = factor;
        }
    }

    public sealed class UnitCategory
    {
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string DefaultFrom { get; set; } = string.Empty;
        public string DefaultTo { get; set; } = string.Empty;
        public string[] Common { get; set; } = Array.Empty<string>();
        public List<Unit> Units { get; set; } = new();

        public bool IsTemperature => string.Equals(Name, "Temperature", StringComparison.OrdinalIgnoreCase);

        public Unit Find(string symbol)
        {
            return Units.Find(u => string.Equals(u.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                   ?? Units[0];
        }
    }

    public sealed class RecentConversion
    {
        public string Category { get; set; } = string.Empty;
        public string FromSymbol { get; set; } = string.Empty;
        public string ToSymbol { get; set; } = string.Empty;
        public decimal InputValue { get; set; }
        public string ResultText { get; set; } = string.Empty;

        public string DisplayText => $"{InputValue} {FromSymbol} → {ResultText} {ToSymbol}";
    }

    public sealed class UnitPairState
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;

        public UnitPairState() { }

        public UnitPairState(string from, string to)
        {
            From = from ?? string.Empty;
            To = to ?? string.Empty;
        }
    }

    public sealed class UnitsState
    {
        public string Category { get; set; } = "Length";
        public decimal Value { get; set; } = 1m;
        public Dictionary<string, UnitPairState> Pairs { get; set; } = new();
        public List<RecentConversion> Recents { get; set; } = new();
    }
}
