// StrategiesPage.xaml.cs

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.UI;
using ZUI.Services;

namespace ZUI.Views
{
    // ═════════════════════════════════════════════════════════════════════════
    // StrategyItem — ViewModel для одной строки в ListView
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class StrategyItem : INotifyPropertyChanged
    {
        private StrategyTestSnapshot? _snap;

        public string ConfigName   { get; init; } = "";   // "general (ALT3).bat"
        public string DisplayName  { get; init; } = "";   // без .bat

        // ── Computed from snapshot ────────────────────────────────────────────

        public StrategyRating Rating => _snap?.Rating ?? StrategyRating.Unknown;

        public string RatingShortLabel => Rating switch
        {
            StrategyRating.Recommended    => "OK",
            StrategyRating.Acceptable     => "~OK",
            StrategyRating.NotRecommended => "ERR",
            _                             => ""
        };

        public string RatingEmoji => Rating switch
        {
            StrategyRating.Recommended    => "✓",
            StrategyRating.Acceptable     => "~",
            StrategyRating.NotRecommended => "✗",
            _                             => ""
        };

        public string RatingTooltip =>
            _snap is null
                ? "Требуется тестирование"
                : $"{_snap.RatingLabel}\n{_snap.Summary}";

        public Visibility RatingVisible =>
            Rating == StrategyRating.Unknown ? Visibility.Collapsed : Visibility.Visible;

        // ── Colours ───────────────────────────────────────────────────────────

        private static readonly Color _green  = Color.FromArgb(255,  76, 195, 125);
        private static readonly Color _yellow = Color.FromArgb(255, 220, 165,  30);
        private static readonly Color _red    = Color.FromArgb(255, 220,  80,  70);

        private Color AccentColor => Rating switch
        {
            StrategyRating.Recommended    => _green,
            StrategyRating.Acceptable     => _yellow,
            StrategyRating.NotRecommended => _red,
            _                             => Color.FromArgb(255, 130, 130, 140)
        };

        public SolidColorBrush RatingBackground =>
            new(Color.FromArgb(35, AccentColor.R, AccentColor.G, AccentColor.B));

        public SolidColorBrush RatingForeground => new(AccentColor);

        // ── Update ────────────────────────────────────────────────────────────

        public void ApplySnapshot(StrategyTestSnapshot? snap)
        {
            _snap = snap;
            OnPropertyChanged(nameof(Rating));
            OnPropertyChanged(nameof(RatingShortLabel));
            OnPropertyChanged(nameof(RatingEmoji));
            OnPropertyChanged(nameof(RatingTooltip));
            OnPropertyChanged(nameof(RatingVisible));
            OnPropertyChanged(nameof(RatingBackground));
            OnPropertyChanged(nameof(RatingForeground));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Page
    // ═════════════════════════════════════════════════════════════════════════

    public sealed partial class StrategiesPage : Page
    {
        private WinwsService _service => AppState.WinwsService;

        private string _selectedBatPath = "";
        private readonly List<StrategyItem> _items = [];

        private SolidColorBrush _testResultBorderBrush = new(Colors.Transparent);
        private SolidColorBrush _testResultBackground   = new(Colors.Transparent);
        private SolidColorBrush _testIconBackground     = new(Colors.Transparent);

        private static readonly Dictionary<string, (string Type, string Usage)> StrategyInfo = new()
        {
            ["general"] = ("Основная", "Discord, YouTube, Google. Рекомендуется для большинства пользователей."),
            ["general (ALT)"] = ("Альтернативная 1", "Используй если general не работает."),
            ["general (ALT2)"] = ("Альтернативная 2", "Используй если ALT не помог."),
            ["general (ALT3)"] = ("Альтернативная 3", "Ещё один вариант обхода DPI."),
            ["general (ALT4)"] = ("Альтернативная 4", "Для провайдеров с глубокой инспекцией."),
            ["general (ALT5)"] = ("Альтернативная 5", "Лёгкая версия без лишних фильтров."),
            ["general (ALT6)"] = ("Альтернативная 6", "Комбинированный метод обхода."),
            ["general (ALT7)"] = ("Альтернативная 7", "Расширенные параметры десинхронизации."),
            ["general (ALT8)"] = ("Альтернативная 8", "Усиленная версия для сложных провайдеров."),
            ["general (ALT9)"] = ("Альтернативная 9", "Максимальная совместимость."),
            ["general (ALT10)"] = ("Альтернативная 10", "Экспериментальный метод."),
            ["general (ALT11)"] = ("Альтернативная 11", "Для специфических провайдеров."),
            ["general (FAKE TLS AUTO)"] = ("Fake TLS Auto", "Автоматический подбор Fake TLS параметров."),
            ["general (FAKE TLS AUTO ALT)"] = ("Fake TLS Auto ALT", "Альтернативный Fake TLS."),
            ["general (FAKE TLS AUTO ALT2)"] = ("Fake TLS Auto ALT2", "Второй альтернативный Fake TLS."),
            ["general (FAKE TLS AUTO ALT3)"] = ("Fake TLS Auto ALT3", "Третий альтернативный Fake TLS."),
            ["general (SIMPLE FAKE)"] = ("Simple Fake", "Простой метод подмены пакетов."),
            ["general (SIMPLE FAKE ALT)"] = ("Simple Fake ALT", "Альтернативный простой метод."),
            ["general (SIMPLE FAKE ALT2)"] = ("Simple Fake ALT2", "Второй альтернативный простой метод."),
        };

        public StrategiesPage()
        {
            this.InitializeComponent();

            TestResultStore.TryLoadCache();
            LoadStrategies();

            TestResultStore.ResultsUpdated += OnResultsUpdated;
            this.Unloaded += (_, _) => TestResultStore.ResultsUpdated -= OnResultsUpdated;
        }

        // ─────────────────────────────────────────────────────────────────────
        // LOAD
        // ─────────────────────────────────────────────────────────────────────

        private void LoadStrategies()
        {
            if (!Directory.Exists(ZapretPaths.StrategiesDir)) return;

            var batFiles = Directory.GetFiles(ZapretPaths.StrategiesDir, "*.bat")
                                    .OrderBy(f => f)
                                    .ToList();
            _items.Clear();
            StrategyListView.Items.Clear();

            foreach (var bat in batFiles)
            {
                var configName  = Path.GetFileName(bat);
                var displayName = Path.GetFileNameWithoutExtension(bat);

                var item = new StrategyItem
                {
                    ConfigName  = configName,
                    DisplayName = displayName
                };

                var snap = TestResultStore.Get(configName)
                        ?? TestResultStore.Get(displayName);
                item.ApplySnapshot(snap);

                _items.Add(item);
                StrategyListView.Items.Add(item);
            }

            UpdateCountBadge(_items.Count);

            if (StrategyListView.Items.Count > 0)
                StrategyListView.SelectedIndex = 0;
        }

        // ─────────────────────────────────────────────────────────────────────
        // SEARCH
        // ─────────────────────────────────────────────────────────────────────

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            FilterStrategies(sender.Text);
        }

        private void FilterStrategies(string query)
        {
            StrategyListView.Items.Clear();

            var filtered = string.IsNullOrWhiteSpace(query)
                ? _items
                : _items.Where(i => i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .ToList();

            foreach (var item in filtered)
                StrategyListView.Items.Add(item);

            UpdateCountBadge(filtered.Count);

            // Keep selection if filtered item still visible
            if (StrategyListView.Items.Count > 0 && StrategyListView.SelectedIndex < 0)
                StrategyListView.SelectedIndex = 0;
        }

        private void UpdateCountBadge(int count)
        {
            StrategyCountText.Text = count.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        // RATINGS UPDATE
        // ─────────────────────────────────────────────────────────────────────

        private void OnResultsUpdated()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var item in _items)
                {
                    var snap = TestResultStore.Get(item.ConfigName)
                            ?? TestResultStore.Get(item.DisplayName);
                    item.ApplySnapshot(snap);
                }

                if (StrategyListView.SelectedItem is StrategyItem sel)
                    ShowDetailCard(sel);
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // SELECTION
        // ─────────────────────────────────────────────────────────────────────

        private void StrategyListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StrategyListView.SelectedItem is not StrategyItem item)
            {
                ShowEmptyState(true);
                return;
            }

            ShowEmptyState(false);

            _selectedBatPath = Path.Combine(ZapretPaths.StrategiesDir, item.ConfigName);
            var name = item.DisplayName;

            StrategyTitleText.Text = name;
            StrategyInfoPanel.Visibility = Visibility.Visible;
            StrategyDescText.Text = "";

            if (StrategyInfo.TryGetValue(name, out var info))
            {
                StrategyTypeText.Text  = info.Type;
                StrategyUsageText.Text = info.Usage;
            }
            else
            {
                StrategyTypeText.Text  = "Стратегия zapret";
                StrategyUsageText.Text = "Обход DPI для Discord и YouTube.";
            }

            StatusInfoBar.IsOpen = AppState.CurrentStrategy == name;
            if (StatusInfoBar.IsOpen)
            {
                StatusInfoBar.Severity = InfoBarSeverity.Success;
                StatusInfoBar.Message  = "Эта стратегия сейчас активна";
            }

            ShowDetailCard(item);
        }

        private void ShowEmptyState(bool empty)
        {
            EmptyState.Visibility    = empty ? Visibility.Visible   : Visibility.Collapsed;
            DetailContent.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        }

        // ─────────────────────────────────────────────────────────────────────
        // DETAIL CARD
        // ─────────────────────────────────────────────────────────────────────

        private void ShowDetailCard(StrategyItem item)
        {
            var snap = TestResultStore.Get(item.ConfigName)
                    ?? TestResultStore.Get(item.DisplayName);

            if (snap is null)
            {
                TestResultCard.Visibility = Visibility.Collapsed;
                return;
            }

            TestResultCard.Visibility = Visibility.Visible;

            var (accent, bgAlpha, emoji, label) = snap.Rating switch
            {
                StrategyRating.Recommended    =>
                    (Color.FromArgb(255, 76, 195, 125), (byte)30, "✓", "Рекомендуется"),
                StrategyRating.Acceptable     =>
                    (Color.FromArgb(255, 220, 165, 30), (byte)30, "~", "Приемлемо"),
                StrategyRating.NotRecommended =>
                    (Color.FromArgb(255, 220, 80, 70),  (byte)30, "✗", "Не рекомендуется"),
                _ =>
                    (Color.FromArgb(255, 130, 130, 140), (byte)20, "?", "Требуется тестирование")
            };

            _testResultBorderBrush = new SolidColorBrush(Color.FromArgb(60, accent.R, accent.G, accent.B));
            _testResultBackground  = new SolidColorBrush(Color.FromArgb(bgAlpha, accent.R, accent.G, accent.B));
            _testIconBackground    = new SolidColorBrush(Color.FromArgb(50, accent.R, accent.G, accent.B));

            TestResultCard.BorderBrush = _testResultBorderBrush;
            TestResultCard.Background  = _testResultBackground;
            TestResultIcon.Background  = _testIconBackground;

            TestResultEmoji.Text       = emoji;
            TestResultEmoji.Foreground = new SolidColorBrush(accent);
            TestResultLabel.Text       = label;
            TestResultLabel.Foreground = new SolidColorBrush(accent);

            if (snap.Mode == TestMode.Standard)
            {
                TestResultSummary.Text = snap.IsBest
                    ? $"★ Лучший результат теста  •  OK {snap.HttpOk}  ERR {snap.HttpErr}  Ping {snap.PingOk}"
                    : $"OK {snap.HttpOk}  ERR {snap.HttpErr}  UNSUP {snap.HttpUnsup}  Ping {snap.PingOk}";
            }
            else
            {
                TestResultSummary.Text = snap.IsBest
                    ? $"★ Лучший результат DPI теста  •  OK {snap.DpiOk}  BLOCKED {snap.DpiBlocked}"
                    : $"DPI OK {snap.DpiOk}  BLOCKED {snap.DpiBlocked}";
            }

            TestResultDate.Text = $"Протестировано: {snap.TestedAt:dd.MM.yyyy HH:mm}";
        }

        // ─────────────────────────────────────────────────────────────────────
        // APPLY STRATEGY
        // ─────────────────────────────────────────────────────────────────────

        private async void ApplyStrategyButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedBatPath)) return;

            var arguments = BatStrategyParser.ParseStrategy(_selectedBatPath);
            if (string.IsNullOrEmpty(arguments))
            {
                await new ContentDialog
                {
                    Title           = "Ошибка",
                    Content         = "Не удалось прочитать стратегию из файла.",
                    CloseButtonText = "OK",
                    XamlRoot        = this.XamlRoot
                }.ShowAsync();
                return;
            }

            _service.Stop();
            await _service.StartAsync(arguments);

            if (StrategyListView.SelectedItem is StrategyItem sel)
                AppState.CurrentStrategy = sel.DisplayName;

            StatusInfoBar.IsOpen   = true;
            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.Message  = "Эта стратегия сейчас активна";
        }

        // ─────────────────────────────────────────────────────────────────────
        // CLEAR RATINGS
        // ─────────────────────────────────────────────────────────────────────

        private void ClearRatings_Click(object sender, RoutedEventArgs e)
        {
            TestResultStore.ClearCache();
        }
    }
}
