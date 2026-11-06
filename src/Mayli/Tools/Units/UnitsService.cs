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
        private readonly Dictionary<string, UnitPairState> _categoryPairs = new(StringComparer.OrdinalIgnoreCase);

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
                        InputValue = saved.Value != 0 ? saved.Value : 1m;

                        _categoryPairs.Clear();
                        if (saved.Pairs != null)
                        {
                            foreach (var kvp in saved.Pairs)
                            {
                                if (kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.From) && !string.IsNullOrEmpty(kvp.Value.To))
                                {
                                    _categoryPairs[kvp.Key] = kvp.Value;
                                }
                            }
                        }

                        if (_categoryPairs.TryGetValue(ActiveCategory.Name, out var p) && !string.IsNullOrEmpty(p.From) && !string.IsNullOrEmpty(p.To))
                        {
                            FromUnit = ActiveCategory.Find(p.From);
                            ToUnit = ActiveCategory.Find(p.To);
                        }
                        else
                        {
                            FromUnit = ActiveCategory.Find(ActiveCategory.DefaultFrom);
                            ToUnit = ActiveCategory.Find(ActiveCategory.DefaultTo);
                        }

                        if (FromUnit != null && ToUnit != null && string.Equals(FromUnit.Symbol, ToUnit.Symbol, StringComparison.OrdinalIgnoreCase) && ActiveCategory.Units.Count > 1)
                        {
                            ToUnit = ActiveCategory.Units.FirstOrDefault(u => !string.Equals(u.Symbol, FromUnit.Symbol, StringComparison.OrdinalIgnoreCase)) ?? ActiveCategory.Units[1];
                        }
                        _categoryPairs[ActiveCategory.Name] = new UnitPairState(FromUnit.Symbol, ToUnit.Symbol);

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
            _categoryPairs["Length"] = new UnitPairState("m", "ft");
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
                        Pairs = new Dictionary<string, UnitPairState>(_categoryPairs, StringComparer.OrdinalIgnoreCase),
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
                if (cat == null) return;
                if (ActiveCategory != null && string.Equals(cat.Name, ActiveCategory.Name, StringComparison.OrdinalIgnoreCase))
                    return;

                ActiveCategory = cat;

                if (_categoryPairs.TryGetValue(cat.Name, out var p) && !string.IsNullOrEmpty(p.From) && !string.IsNullOrEmpty(p.To))
                {
                    FromUnit = cat.Find(p.From);
                    ToUnit = cat.Find(p.To);
                }
                else
                {
                    FromUnit = cat.Find(cat.DefaultFrom);
                    ToUnit = cat.Find(cat.DefaultTo);
                }

                if (FromUnit != null && ToUnit != null && string.Equals(FromUnit.Symbol, ToUnit.Symbol, StringComparison.OrdinalIgnoreCase) && cat.Units.Count > 1)
                {
                    ToUnit = cat.Units.FirstOrDefault(u => !string.Equals(u.Symbol, FromUnit.Symbol, StringComparison.OrdinalIgnoreCase)) ?? cat.Units[1];
                }

                _categoryPairs[cat.Name] = new UnitPairState(FromUnit.Symbol, ToUnit.Symbol);

                CommitRecent();
                Save();
                StateChanged?.Invoke();
            }
        }

        public void SetFromUnit(Unit u)
        {
            if (u == null) return;
            lock (_lock)
            {
                FromUnit = u;
                if (ActiveCategory != null)
                {
                    _categoryPairs[ActiveCategory.Name] = new UnitPairState(FromUnit.Symbol, ToUnit?.Symbol ?? "");
                }
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
                if (ActiveCategory != null)
                {
                    _categoryPairs[ActiveCategory.Name] = new UnitPairState(FromUnit?.Symbol ?? "", ToUnit.Symbol);
                }
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
                if (ActiveCategory != null && FromUnit != null && ToUnit != null)
                {
                    _categoryPairs[ActiveCategory.Name] = new UnitPairState(FromUnit.Symbol, ToUnit.Symbol);
                }
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
