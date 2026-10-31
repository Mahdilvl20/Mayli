using System;
using System.Collections.Generic;
using System.Linq;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Units
{
    public sealed class UnitsService
    {
        private static readonly Lazy<UnitsService> _instance = new(() => new UnitsService());
        public static UnitsService Instance => _instance.Value;

        private const string PrefsFileName = "units_prefs.json";
        private readonly object _lock = new();

        public event Action StateChanged;

        public UnitCategory ActiveCategory { get; private set; }
        public Unit FromUnit { get; private set; }
        public Unit ToUnit { get; private set; }
        public decimal InputValue { get; private set; } = 1m;
        public decimal ConvertedValue => UnitCatalog.Convert(ActiveCategory, FromUnit, ToUnit, InputValue);

        public List<RecentConversion> Recents { get; private set; } = new();

        private UnitsService()
        {
            Load();
        }

        public void Load()
        {
            lock (_lock)
            {
                try
                {
                    var saved = PersistenceHelper.SafeReadJson<UnitsState>(PrefsFileName);
                    if (saved != null)
                    {
                        ActiveCategory = UnitCatalog.FindCategory(saved.Category);
                        InputValue = saved.Value > 0 ? saved.Value : 1m;

                        if (saved.Pairs != null && saved.Pairs.TryGetValue(ActiveCategory.Name, out var p))
                        {
                            FromUnit = ActiveCategory.Find(p.from);
                            ToUnit = ActiveCategory.Find(p.to);
                        }
                        else
                        {
                            FromUnit = ActiveCategory.Find(ActiveCategory.DefaultFrom);
                            ToUnit = ActiveCategory.Find(ActiveCategory.DefaultTo);
                        }

                        Recents = saved.Recents ?? new List<RecentConversion>();
                    }
                    else
                    {
                        InitDefaults();
                    }
                }
                catch
                {
                    InitDefaults();
                }
            }
        }

        private void InitDefaults()
        {
            ActiveCategory = UnitCatalog.FindCategory("Length");
            FromUnit = ActiveCategory.Find("m");
            ToUnit = ActiveCategory.Find("ft");
            InputValue = 1m;
            Recents = new List<RecentConversion>
            {
                new RecentConversion
                {
                    Category = "Length",
                    FromSymbol = "m",
                    ToSymbol = "ft",
                    InputValue = 1m,
                    ResultText = "3.28084"
                }
            };
            Save();
        }

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    var state = new UnitsState
                    {
                        Category = ActiveCategory?.Name ?? "Length",
                        Value = InputValue,
                        Pairs = new Dictionary<string, (string from, string to)>
                        {
                            [ActiveCategory.Name] = (FromUnit.Symbol, ToUnit.Symbol)
                        },
                        Recents = Recents
                    };
                    PersistenceHelper.SafeWriteJson(PrefsFileName, state);
                }
                catch { }
            }
        }

        public void SelectCategory(string catName)
        {
            lock (_lock)
            {
                var cat = UnitCatalog.FindCategory(catName);
                if (cat != null && cat != ActiveCategory)
                {
                    ActiveCategory = cat;
                    FromUnit = cat.Find(cat.DefaultFrom);
                    ToUnit = cat.Find(cat.DefaultTo);
                    CommitRecent();
                    Save();
                    StateChanged?.Invoke();
                }
            }
        }

        public void SetFromUnit(Unit u)
        {
            if (u == null) return;
            lock (_lock)
            {
                FromUnit = u;
                CommitRecent();
                Save();
                StateChanged?.Invoke();
            }
        }

        public void SetToUnit(Unit u)
        {
            if (u == null) return;
            lock (_lock)
            {
                ToUnit = u;
                CommitRecent();
                Save();
                StateChanged?.Invoke();
            }
        }

        public void SetInputValue(decimal val)
        {
            lock (_lock)
            {
                InputValue = val;
                CommitRecent();
                Save();
                StateChanged?.Invoke();
            }
        }

        public void Swap()
        {
            lock (_lock)
            {
                var temp = FromUnit;
                FromUnit = ToUnit;
                ToUnit = temp;
                CommitRecent();
                Save();
                StateChanged?.Invoke();
            }
        }

        public List<(string name, string symbol, string value)> GetCommonEquivalents()
        {
            var list = new List<(string name, string symbol, string value)>();
            if (ActiveCategory == null || ActiveCategory.Common == null) return list;

            foreach (var sym in ActiveCategory.Common)
            {
                if (string.Equals(sym, ToUnit?.Symbol, StringComparison.OrdinalIgnoreCase))
                    continue;

                var targetUnit = ActiveCategory.Find(sym);
                if (targetUnit != null && targetUnit != FromUnit)
                {
                    decimal converted = UnitCatalog.Convert(ActiveCategory, FromUnit, targetUnit, InputValue);
                    list.Add((targetUnit.Name, targetUnit.Symbol, UnitCatalog.FormatResult(converted)));
                    if (list.Count >= 3) break;
                }
            }

            return list;
        }

        private void CommitRecent()
        {
            if (FromUnit == null || ToUnit == null) return;

            string res = UnitCatalog.FormatResult(ConvertedValue);
            var item = new RecentConversion
            {
                Category = ActiveCategory.Name,
                FromSymbol = FromUnit.Symbol,
                ToSymbol = ToUnit.Symbol,
                InputValue = InputValue,
                ResultText = res
            };

            Recents.RemoveAll(r => r.Category == item.Category && r.FromSymbol == item.FromSymbol && r.ToSymbol == item.ToSymbol && r.InputValue == item.InputValue);
            Recents.Insert(0, item);
            if (Recents.Count > 5) Recents.RemoveAt(Recents.Count - 1);
        }
    }
}
