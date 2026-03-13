// Services/TestResultStore.cs
// Singleton-хранилище результатов тестирования.
// ServicesPage.Testing.cs пишет сюда после теста,
// StrategiesPage читает и отображает значки.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;                          // ← ДОБАВЛЕНО: SelectMany, Count и т.д.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZUI.Services
{
    // ── Rating ────────────────────────────────────────────────────────────────

    public enum StrategyRating
    {
        Unknown,        // ещё не тестировалась  → серый  «Требуется тестирование»
        Recommended,    // лучший конфиг или OK ≥ 80%  → зелёный «Рекомендуется»
        Acceptable,     // OK ≥ 50%  → жёлтый «Приемлемо»
        NotRecommended  // OK < 50%  → красный «Не рекомендуется»
    }

    // ── Per-strategy snapshot ─────────────────────────────────────────────────

    public class StrategyTestSnapshot
    {
        public string        ConfigName   { get; set; } = "";
        public StrategyRating Rating      { get; set; } = StrategyRating.Unknown;
        public bool          IsBest       { get; set; }
        public int           HttpOk       { get; set; }
        public int           HttpErr      { get; set; }
        public int           HttpUnsup    { get; set; }
        public int           PingOk       { get; set; }
        public int           DpiOk        { get; set; }
        public int           DpiBlocked   { get; set; }
        public TestMode      Mode         { get; set; }
        public DateTime      TestedAt     { get; set; }

        [JsonIgnore]
        public string RatingLabel => Rating switch
        {
            StrategyRating.Recommended    => "Рекомендуется",
            StrategyRating.Acceptable     => "Приемлемо",
            StrategyRating.NotRecommended => "Не рекомендуется",
            _                             => "Требуется тестирование"
        };

        [JsonIgnore]
        public string RatingEmoji => Rating switch
        {
            StrategyRating.Recommended    => "✓",
            StrategyRating.Acceptable     => "~",
            StrategyRating.NotRecommended => "✗",
            _                             => "?"
        };

        /// <summary>Short summary line for StrategiesPage tooltip.</summary>
        [JsonIgnore]
        public string Summary => Mode == TestMode.Standard
            ? $"OK {HttpOk}  ERR {HttpErr}  Ping {PingOk}  —  {TestedAt:dd.MM HH:mm}"
            : $"DPI OK {DpiOk}  BLOCKED {DpiBlocked}  —  {TestedAt:dd.MM HH:mm}";
    }

    // ── Store ─────────────────────────────────────────────────────────────────

    public static class TestResultStore
    {
        // Fired on UI thread when results are updated
        public static event Action? ResultsUpdated;

        private static readonly Dictionary<string, StrategyTestSnapshot> _results = new();
        private static readonly object _lock = new();

        private static string CachePath =>
            Path.Combine(ZapretPaths.UtilsDir, "test_results_cache.json");

        // ── Write (called from ServicesPage.Testing after RunAsync) ───────────

        public static void Publish(
            IReadOnlyList<ConfigResult> results,
            TestMode mode)
        {
            if (results.Count == 0) return;

            var best = ZapretTestRunner.FindBest(results, mode);

            lock (_lock)
            {
                foreach (var r in results)
                {
                    int total = mode == TestMode.Standard
                        ? r.Standard.SelectMany(t => t.Http).Count()
                        : r.Dpi.SelectMany(d => d.Lines).Count();
                    int ok = mode == TestMode.Standard ? r.HttpOk : r.DpiOk;

                    double ratio = total == 0 ? 0 : (double)ok / total;

                    var rating = r.ConfigName == best?.ConfigName
                        ? StrategyRating.Recommended
                        : ratio >= 0.8
                            ? StrategyRating.Recommended
                            : ratio >= 0.5
                                ? StrategyRating.Acceptable
                                : StrategyRating.NotRecommended;

                    _results[r.ConfigName] = new StrategyTestSnapshot
                    {
                        ConfigName  = r.ConfigName,
                        Rating      = rating,
                        IsBest      = r.ConfigName == best?.ConfigName,
                        HttpOk      = r.HttpOk,
                        HttpErr     = r.HttpErr,
                        HttpUnsup   = r.HttpUnsup,
                        PingOk      = r.PingOk,
                        DpiOk       = r.DpiOk,
                        DpiBlocked  = r.DpiBlocked,
                        Mode        = mode,
                        TestedAt    = DateTime.Now
                    };
                }
            }

            TrySaveCache();
            ResultsUpdated?.Invoke();
        }

        // ── Read (called from StrategiesPage) ─────────────────────────────────

        /// <returns>null if this strategy was never tested.</returns>
        public static StrategyTestSnapshot? Get(string configName)
        {
            lock (_lock)
            {
                _results.TryGetValue(configName, out var snap);
                return snap;
            }
        }

        public static IReadOnlyDictionary<string, StrategyTestSnapshot> GetAll()
        {
            lock (_lock) { return new Dictionary<string, StrategyTestSnapshot>(_results); }
        }

        public static bool HasAnyResults()
        {
            lock (_lock) { return _results.Count > 0; }
        }

        // ── Persistence (survives app restart) ───────────────────────────────

        public static void TryLoadCache()
        {
            try
            {
                if (!File.Exists(CachePath)) return;
                var json = File.ReadAllText(CachePath);
                var list = JsonSerializer.Deserialize<List<StrategyTestSnapshot>>(json);
                if (list is null) return;
                lock (_lock)
                {
                    foreach (var s in list)
                        _results[s.ConfigName] = s;
                }
            }
            catch { }
        }

        private static void TrySaveCache()
        {
            try
            {
                List<StrategyTestSnapshot> list;
                lock (_lock) { list = new List<StrategyTestSnapshot>(_results.Values); }
                var json = JsonSerializer.Serialize(list,
                    new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(CachePath, json);
            }
            catch { }
        }

        public static void ClearCache()
        {
            lock (_lock) { _results.Clear(); }
            try { File.Delete(CachePath); } catch { }
            ResultsUpdated?.Invoke();
        }
    }
}
