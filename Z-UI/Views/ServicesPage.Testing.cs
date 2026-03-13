// ServicesPage.Testing.cs — partial class
// Красивый вывод результатов тестирования для вкладки «Тестирование»

using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;  // ← ДОБАВЛЕНО: ToggleButton
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using ZUI.Services;

namespace ZUI.Views
{
    public sealed partial class ServicesPage
    {
        // ── Runtime state ─────────────────────────────────────────────────────
        private ZapretTestRunner?                      _runner;
        private CancellationTokenSource?               _testCts;
        private IReadOnlyList<FileInfo>                _allConfigs  = [];
        private IReadOnlyList<TestTarget>              _targets     = [];
        private IReadOnlyList<DpiTester.DpiSuiteEntry> _dpiSuite   = [];
        private IReadOnlyList<ConfigResult>            _lastResults = [];
        private ConfigResult?                          _bestResult;
        private TestMode                               _currentMode = TestMode.Standard;

        // ── Colour palette ────────────────────────────────────────────────────
        private static readonly Color _colOk      = Color.FromArgb(255,  76, 195, 125);
        private static readonly Color _colError    = Color.FromArgb(255, 220,  80,  70);
        private static readonly Color _colWarn     = Color.FromArgb(255, 220, 165,  30);
        private static readonly Color _colUnsup    = Color.FromArgb(255, 140, 140, 160);
        private static readonly Color _colInfo     = Color.FromArgb(255, 100, 165, 220);
        private static readonly Color _colDim      = Color.FromArgb(255, 120, 120, 135);
        private static readonly Color _colHeading  = Color.FromArgb(255, 200, 200, 215);

        // ═════════════════════════════════════════════════════════════════════
        // INIT
        // ═════════════════════════════════════════════════════════════════════

        private void InitTestingTab()
        {
            _runner = new ZapretTestRunner(ZapretPaths.AppDir, ZapretPaths.ListsDir, ZapretPaths.StrategiesDir);
            _runner.Log             += msg  => AppendLog(msg);
            _runner.ProgressChanged += prog => OnTestProgress(prog);

            var ipset = new IpsetManager(ZapretPaths.ListsDir);
            if (ipset.RecoverIfNeeded())
                AppendLog("[INFO] ipset восстановлен после аварийного завершения", _colWarn);

            RefreshConfigChips();
        }

        // ═════════════════════════════════════════════════════════════════════
        // CONFIG CHIPS
        // ═════════════════════════════════════════════════════════════════════

        private void RefreshConfigChips()
        {
            _allConfigs = _runner!.GetConfigs(ZapretPaths.StrategiesDir);
            ConfigChipsList.Items.Clear();
        
            foreach (var cfg in _allConfigs)
            {
                var label = cfg.Name.Replace("general", "").Replace(".bat", "").Trim('(', ')', ' ');
                if (string.IsNullOrEmpty(label)) label = "base";
        
                var cb = new CheckBox
                {
                    Content             = label,
                    Tag                 = cfg,
                    IsChecked           = true,
                    Padding             = new Thickness(8, 6, 8, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                ToolTipService.SetToolTip(cb, cfg.Name);
                cb.Checked   += (_, _) => UpdateSelectedCount();
                cb.Unchecked += (_, _) => UpdateSelectedCount();
                ConfigChipsList.Items.Add(cb);
            }
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            var selected = GetSelectedConfigs().Count;
            var total    = _allConfigs.Count;
        
            SelectedCountText.Text = $"{selected} выбрано";
        
            if (selected == total)
            {
                StrategiesDropDownLabel.Text    = "Все стратегии";
                StrategiesBadge.Visibility      = Visibility.Collapsed;
            }
            else if (selected == 0)
            {
                StrategiesDropDownLabel.Text    = "Нет выбранных";
                StrategiesBadge.Visibility      = Visibility.Collapsed;
            }
            else
            {
                StrategiesDropDownLabel.Text    = "Выбрать стратегии";
                StrategiesBadgeText.Text        = selected.ToString();
                StrategiesBadge.Visibility      = Visibility.Visible;
            }
        }

        private IReadOnlyList<FileInfo> GetSelectedConfigs() =>
            ConfigChipsList.Items
                .OfType<CheckBox>()
                .Where(b => b.IsChecked == true)
                .Select(b => (FileInfo)b.Tag!)
                .ToList();
        private void SelectAllStrategies_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigChipsList?.Items is null) return;
            foreach (CheckBox cb in ConfigChipsList.Items.OfType<CheckBox>()) cb.IsChecked = true;
        }
        
        private void ClearAllStrategies_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigChipsList?.Items is null) return;
            foreach (CheckBox cb in ConfigChipsList.Items.OfType<CheckBox>()) cb.IsChecked = false;
        }
        private void SelectAllToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (ConfigChipsList?.Items is null) return;
            foreach (ToggleButton b in ConfigChipsList.Items) b.IsChecked = false;
        }
        private void ModeChanged(object sender, RoutedEventArgs e)
        {
            _currentMode = ModeDpi?.IsChecked == true ? TestMode.Dpi : TestMode.Standard;
        }
        private void OutputTab_CloseRequested(TabView _, TabViewTabCloseRequestedEventArgs __) { }

        // ═════════════════════════════════════════════════════════════════════
        // RUN / STOP
        // ═════════════════════════════════════════════════════════════════════

        private async void RunTest_Click(object sender, RoutedEventArgs e)
        {
            var preflight = _runner!.Preflight(_currentMode);
            if (!preflight.Ok)
            {
                foreach (var err in preflight.Errors)
                    AppendLog($"[ОШИБКА] {err}", _colError);
                return;
            }
            foreach (var w in preflight.Warnings)
                AppendLog($"[ПРЕДУПРЕЖДЕНИЕ] {w}", _colWarn);

            var selected = GetSelectedConfigs();
            if (selected.Count == 0) { AppendLog("[ОШИБКА] Не выбрана ни одна стратегия", _colError); return; }

            SetRunning();
            ClearLog();
            ResultsPanel.Children.Clear();
            BestConfigBar.IsOpen       = false;
            ResultsTab.IsEnabled       = false;
            OutputTabView.SelectedItem = LogTab;

            AppendLogSeparator();
            AppendLog($"  Запуск тестирования  {DateTime.Now:HH:mm:ss}", _colHeading, bold: true, size: 13);
            AppendLog($"  Режим:     {(_currentMode == TestMode.Standard ? "Standard — HTTP + Ping" : "DPI — TCP 16-20 KB freeze")}", _colInfo);
            AppendLog($"  Стратегий: {selected.Count}", _colInfo);
            AppendLog($"  Защита:    {(AppState.WinwsService.IsRunning ? "✓ АКТИВНА" : "○ ОСТАНОВЛЕНА")}",
                AppState.WinwsService.IsRunning ? _colOk : _colWarn);
            AppendLogSeparator();

            bool serviceWasInstalled = ServiceManager.IsInstalled();
            string? serviceStrategy  = null;
            if (serviceWasInstalled)
            {
                serviceStrategy = ServiceManager.GetInstalledStrategy();
                AppendLog($"[SVC] Служба установлена ({serviceStrategy}) — временно удаляю...", _colWarn);
                await ServiceManager.RemoveAsync(l => AppendLog("[SVC] " + l, _colDim));
            }

            _targets = _runner.LoadTargets(Path.Combine(ZapretPaths.UtilsDir, "targets.txt"));

            if (_currentMode == TestMode.Dpi)
            {
                AppendLog("[DPI] Загружаю suite...", _colDim);
                _dpiSuite = await new DpiTester().LoadSuiteAsync();
                AppendLog(_dpiSuite.Count > 0
                    ? $"[DPI] Suite загружен: {_dpiSuite.Count} целей"
                    : "[DPI] Suite недоступен", _dpiSuite.Count > 0 ? _colOk : _colWarn);
            }

            _testCts = new CancellationTokenSource();
            try
            {
                _lastResults = await _runner.RunAsync(
                    _currentMode, selected, _targets, _dpiSuite,
                    _testCts.Token);

                BuildResultsCards(_lastResults, _currentMode);

                _bestResult = ZapretTestRunner.FindBest(_lastResults, _currentMode);
                if (_bestResult is not null) ShowBestBar(_bestResult.ConfigName);

                TestResultStore.Publish(_lastResults, _currentMode);

                _runner.SaveResults(_lastResults, _currentMode,
                    Path.Combine(ZapretPaths.UtilsDir, "test results"));

                ResultsTab.IsEnabled       = true;
                OutputTabView.SelectedItem = ResultsTab;
            }
            catch (OperationCanceledException) { AppendLog("[ТЕСТ] Остановлено", _colWarn); }
            catch (Exception ex)               { AppendLog($"[ОШИБКА] {ex.Message}", _colError); }

            if (serviceWasInstalled && serviceStrategy is not null)
            {
                AppendLog("[SVC] Восстанавливаю службу...", _colDim);
                var bat  = Path.Combine(ZapretPaths.StrategiesDir, serviceStrategy + ".bat");
                var args = BatStrategyParser.ParseStrategy(bat);
                if (args is not null)
                {
                    await ServiceManager.InstallAsync(serviceStrategy, args, l => AppendLog("[SVC] " + l, _colDim));
                    AppendLog("[SVC] Служба восстановлена ✓", _colOk);
                }
            }

            AppendLogSeparator();
            SetIdle();
        }

        private void StopTest_Click(object sender, RoutedEventArgs e)
        {
            _testCts?.Cancel();
            AppendLog("[ТЕСТ] Остановлено пользователем", _colWarn);
            SetIdle();
        }

        private void ClearTest_Click(object sender, RoutedEventArgs e)
        {
            ClearLog();
            ResultsPanel.Children.Clear();
            BestConfigBar.IsOpen       = false;
            ResultsTab.IsEnabled       = false;
            OutputTabView.SelectedItem = LogTab;
        }

        private async void DeleteServices_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Удаляю службы...");
            await ServiceManager.RemoveAsync(msg => AppendLog("[SVC] " + msg, _colDim));
            AppendLog("Службы удалены.");
        }

        // ═════════════════════════════════════════════════════════════════════
        // RESULTS CARDS
        // ═════════════════════════════════════════════════════════════════════

        private void BuildResultsCards(IReadOnlyList<ConfigResult> results, TestMode mode)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ResultsPanel.Children.Clear();
                ResultsPanel.Children.Add(BuildSummaryCard(results, mode));
                foreach (var r in results)
                    ResultsPanel.Children.Add(BuildConfigCard(r, mode));
            });
        }

        private UIElement BuildSummaryCard(IReadOnlyList<ConfigResult> results, TestMode mode)
        {
            var border = new Border
            {
                CornerRadius    = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                Background      = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)),
                Padding         = new Thickness(16, 12, 16, 14),
                Margin          = new Thickness(0, 0, 0, 4)
            };

            var root = new StackPanel { Spacing = 8 };
            root.Children.Add(new TextBlock
            {
                Text    = "Аналитика",
                Style   = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                Opacity = 0.65
            });

            var best   = ZapretTestRunner.FindBest(results, mode);
            var sorted = results
                .OrderByDescending(r => mode == TestMode.Standard ? r.HttpOk : r.DpiOk)
                .ThenByDescending(r => r.PingOk)
                .ToList();

            foreach (var r in sorted)
                root.Children.Add(BuildAnalyticsRow(r, mode, r.ConfigName == best?.ConfigName));

            border.Child = root;
            return border;
        }

        private UIElement BuildAnalyticsRow(ConfigResult r, TestMode mode, bool isBest)
        {
            var row = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });

            var nameTb = new TextBlock
            {
                Text              = ShortName(r.ConfigName),
                Style             = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight        = isBest ? FontWeights.SemiBold : FontWeights.Normal
            };
            Grid.SetColumn(nameTb, 0);

            var chips = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                Spacing           = 4,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (mode == TestMode.Standard)
            {
                chips.Children.Add(StatChip($"OK {r.HttpOk}", _colOk));
                if (r.HttpErr   > 0) chips.Children.Add(StatChip($"ERR {r.HttpErr}",     _colError));
                if (r.HttpUnsup > 0) chips.Children.Add(StatChip($"UNSUP {r.HttpUnsup}", _colUnsup));
                chips.Children.Add(StatChip($"Ping {r.PingOk}", _colInfo));
            }
            else
            {
                chips.Children.Add(StatChip($"OK {r.DpiOk}", _colOk));
                if (r.DpiBlocked > 0) chips.Children.Add(StatChip($"BLOCKED {r.DpiBlocked}", _colError));
            }
            Grid.SetColumn(chips, 1);

            var star = new TextBlock
            {
                Text              = isBest ? "★" : "",
                Foreground        = new SolidColorBrush(Color.FromArgb(255, 255, 205, 60)),
                FontSize          = 15,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment     = TextAlignment.Center
            };
            Grid.SetColumn(star, 2);

            row.Children.Add(nameTb);
            row.Children.Add(chips);
            row.Children.Add(star);
            return row;
        }

        private UIElement BuildConfigCard(ConfigResult r, TestMode mode)
        {
            var okCount    = mode == TestMode.Standard ? r.HttpOk : r.DpiOk;
            var totalChecks = mode == TestMode.Standard
                ? r.Standard.SelectMany(t => t.Http).Count()
                : r.Dpi.SelectMany(d => d.Lines).Count();
            var isBest = _bestResult?.ConfigName == r.ConfigName;

            var header = new Grid { ColumnSpacing = 10, Padding = new Thickness(0, 2, 0, 2) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = ScoreBadge(okCount, totalChecks);
            Grid.SetColumn(badge, 0);

            var displayName = ShortName(r.ConfigName);
            var nameTb = new TextBlock
            {
                Text              = isBest ? $"★  {displayName}" : displayName,
                Style             = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                VerticalAlignment = VerticalAlignment.Center,
                FontSize          = 13,
                Foreground        = isBest
                    ? new SolidColorBrush(Color.FromArgb(255, 255, 215, 80))
                    : (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"]
            };
            Grid.SetColumn(nameTb, 1);

            var scoreTb = new TextBlock
            {
                Text              = $"{okCount} / {totalChecks}",
                Opacity           = 0.5,
                Style             = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(scoreTb, 2);

            header.Children.Add(badge);
            header.Children.Add(nameTb);
            header.Children.Add(scoreTb);

            return new Expander
            {
                Header                     = header,
                Content                    = mode == TestMode.Standard
                                                 ? BuildStandardContent(r.Standard)
                                                 : BuildDpiContent(r.Dpi),
                IsExpanded                 = isBest,
                HorizontalAlignment        = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Margin                     = new Thickness(0, 0, 0, 4)
            };
        }

        private UIElement BuildStandardContent(IReadOnlyList<TargetResult> targets)
        {
            var grid = new Grid { RowSpacing = 0 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            AddHeaderRow(grid, ["Цель", "HTTP / TLS", "Ping"], colSpan: 3);

            for (int i = 0; i < targets.Count; i++)
            {
                var t   = targets[i];
                var row = i + 1;
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                if (i % 2 == 0) AddZebraStripe(grid, row, 3);

                var nameTb = CellText(t.Target.Name, _colHeading, truncate: true, maxWidth: 168);
                nameTb.Margin = new Thickness(8, 6, 8, 6);
                Grid.SetColumn(nameTb, 0); Grid.SetRow(nameTb, row);
                grid.Children.Add(nameTb);

                var httpRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing     = 4,
                    Margin      = new Thickness(0, 5, 0, 5),
                    VerticalAlignment = VerticalAlignment.Center
                };
                foreach (var h in t.Http)
                    httpRow.Children.Add(HttpChip(h.Label, h.Status));
                if (t.Target.IsPingOnly)
                    httpRow.Children.Add(CellText("ping only", _colDim));
                Grid.SetColumn(httpRow, 1); Grid.SetRow(httpRow, row);
                grid.Children.Add(httpRow);

                var (pingText, pingColor) = t.Ping.Success
                    ? ($"{t.Ping.AvgMs:F0} ms", PingColor(t.Ping.AvgMs))
                    : ("Timeout", _colError);
                var pingTb = CellText(pingText, pingColor);
                pingTb.Margin = new Thickness(4, 6, 8, 6);
                pingTb.TextAlignment = TextAlignment.Right;
                Grid.SetColumn(pingTb, 2); Grid.SetRow(pingTb, row);
                grid.Children.Add(pingTb);
            }

            return new Border { Padding = new Thickness(0, 4, 0, 4), Child = grid };
        }

        private UIElement BuildDpiContent(IReadOnlyList<DpiTargetResult> targets)
        {
            var grid = new Grid { RowSpacing = 0 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

            AddHeaderRow(grid, ["ID", "Провайдер", "HTTP / TLS1.2 / TLS1.3", ""], colSpan: 4);

            for (int i = 0; i < targets.Count; i++)
            {
                var d   = targets[i];
                var row = i + 1;
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                if (i % 2 == 0) AddZebraStripe(grid, row, 4);

                var idTb = CellText(d.Id, _colHeading, truncate: true, maxWidth: 123);
                idTb.Margin = new Thickness(8, 6, 8, 6);
                Grid.SetColumn(idTb, 0); Grid.SetRow(idTb, row);
                grid.Children.Add(idTb);

                var provTb = CellText(d.Provider, _colDim, truncate: true, maxWidth: 103);
                provTb.Margin = new Thickness(0, 6, 8, 6);
                Grid.SetColumn(provTb, 1); Grid.SetRow(provTb, row);
                grid.Children.Add(provTb);

                var lineRow = new StackPanel
                {
                    Orientation       = Orientation.Horizontal,
                    Spacing           = 4,
                    Margin            = new Thickness(0, 5, 0, 5),
                    VerticalAlignment = VerticalAlignment.Center
                };
                foreach (var l in d.Lines) lineRow.Children.Add(DpiChip(l));
                Grid.SetColumn(lineRow, 2); Grid.SetRow(lineRow, row);
                grid.Children.Add(lineRow);

                if (d.HasBlocked)
                {
                    var warn = new TextBlock
                    {
                        Text              = "⚠",
                        Foreground        = new SolidColorBrush(_colWarn),
                        FontSize          = 14,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin            = new Thickness(2, 0, 6, 0)
                    };
                    ToolTipService.SetToolTip(warn, "Паттерн TCP 16-20 KB freeze обнаружен — цензор режет эту стратегию");
                    Grid.SetColumn(warn, 3); Grid.SetRow(warn, row);
                    grid.Children.Add(warn);
                }
            }

            return new Border { Padding = new Thickness(0, 4, 0, 4), Child = grid };
        }

        // ═════════════════════════════════════════════════════════════════════
        // CHIP / BADGE FACTORIES
        // ═════════════════════════════════════════════════════════════════════

        private static Border HttpChip(string label, HttpStatus status)
        {
            var (bg, fg, icon) = status switch
            {
                HttpStatus.Ok          => (Color.FromArgb(35,  76, 195, 125), _colOk,    "✓"),
                HttpStatus.Error       => (Color.FromArgb(35, 220,  80,  70), _colError,  "✗"),
                HttpStatus.SslError    => (Color.FromArgb(35, 220,  80,  70), _colError,  "SSL"),
                HttpStatus.Unsupported => (Color.FromArgb(25, 140, 140, 160), _colUnsup,  "—"),
                _                      => (Color.FromArgb(20, 120, 120, 135), _colDim,   "?")
            };
            var shortLabel = label switch { "TLS1.2" => "1.2", "TLS1.3" => "1.3", _ => label };
            return MakeChip($"{icon} {shortLabel}", fg, bg);
        }

        private static Border DpiChip(DpiLineResult l)
        {
            var (bg, fg, icon) = l.Status switch
            {
                DpiStatus.Ok            => (Color.FromArgb(35,  76, 195, 125), _colOk,   "✓"),
                DpiStatus.LikelyBlocked => (Color.FromArgb(50, 220,  80,  70), _colError, "⚑"),
                DpiStatus.Fail          => (Color.FromArgb(35, 220,  80,  70), _colError, "✗"),
                DpiStatus.Unsupported   => (Color.FromArgb(25, 140, 140, 160), _colUnsup, "—"),
                _                       => (Color.FromArgb(20, 120, 120, 135), _colDim,  "?")
            };
            var shortLabel = l.Label switch { "TLS1.2" => "1.2", "TLS1.3" => "1.3", _ => l.Label };
            var chip = MakeChip($"{icon} {shortLabel}", fg, bg);
            ToolTipService.SetToolTip(chip, $"{l.Label}: {l.Code}  {l.SizeKb:F1} KB  →  {l.Status}");
            return chip;
        }

        private static Border StatChip(string text, Color fg)
        {
            var bg = Color.FromArgb(28, fg.R, fg.G, fg.B);
            return new Border
            {
                Background   = new SolidColorBrush(bg),
                CornerRadius = new CornerRadius(12),
                Padding      = new Thickness(8, 2, 8, 2),
                Child        = new TextBlock
                {
                    Text              = text,
                    Foreground        = new SolidColorBrush(fg),
                    FontSize          = 11.5,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private static Border MakeChip(string text, Color fg, Color bg) => new()
        {
            Background   = new SolidColorBrush(bg),
            CornerRadius = new CornerRadius(4),
            Padding      = new Thickness(6, 2, 6, 2),
            Child        = new TextBlock
            {
                Text              = text,
                Foreground        = new SolidColorBrush(fg),
                FontSize          = 11.5,
                FontFamily        = new FontFamily("Cascadia Mono, Consolas, monospace"),
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        private static Border ScoreBadge(int ok, int total)
        {
            double ratio = total == 0 ? 0 : (double)ok / total;
            var (bg, fg) = ratio >= 0.8
                ? (Color.FromArgb(40,  76, 195, 125), _colOk)
                : ratio >= 0.5
                ? (Color.FromArgb(40, 220, 165,  30), _colWarn)
                : (Color.FromArgb(40, 220,  80,  70), _colError);
            return new Border
            {
                Width        = 38, Height      = 38,
                Background   = new SolidColorBrush(bg),
                CornerRadius = new CornerRadius(19),
                Margin       = new Thickness(0, 0, 4, 0),
                Child        = new TextBlock
                {
                    Text                = $"{ok}",
                    Foreground          = new SolidColorBrush(fg),
                    FontSize            = 14,
                    FontWeight          = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                }
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        // GRID HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private static void AddHeaderRow(Grid grid, string[] labels, int colSpan)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < labels.Length; c++)
            {
                var tb = new TextBlock
                {
                    Text    = labels[c],
                    Style   = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Opacity = 0.4,
                    Margin  = new Thickness(c == 0 ? 8 : 0, 2, 8, 5)
                };
                Grid.SetColumn(tb, c); Grid.SetRow(tb, 0);
                grid.Children.Add(tb);
            }
        }

        private static void AddZebraStripe(Grid grid, int row, int colSpan)
        {
            var stripe = new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(8, 255, 255, 255)),
                CornerRadius = new CornerRadius(4)
            };
            Grid.SetRow(stripe, row);
            Grid.SetColumnSpan(stripe, colSpan);
            grid.Children.Insert(0, stripe);
        }

        private static TextBlock CellText(string text, Color fg,
            bool truncate = false, double maxWidth = 0)
        {
            var tb = new TextBlock
            {
                Text              = text,
                Foreground        = new SolidColorBrush(fg),
                FontSize          = 12,
                FontFamily        = new FontFamily("Cascadia Mono, Consolas, monospace"),
                VerticalAlignment = VerticalAlignment.Center
            };
            if (truncate)
            {
                tb.TextTrimming = TextTrimming.CharacterEllipsis;
                if (maxWidth > 0) tb.MaxWidth = maxWidth;
            }
            return tb;
        }

        private static Color PingColor(double ms) =>
            ms < 50  ? _colOk :
            ms < 150 ? _colWarn :
                       _colError;

        private static string ShortName(string n) => n.Replace(".bat", "");

        // ═════════════════════════════════════════════════════════════════════
        // BEST CONFIG BAR
        // ═════════════════════════════════════════════════════════════════════

        private void ShowBestBar(string name)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                BestConfigBar.Title   = $"★  Лучший конфиг:  {ShortName(name)}";
                BestConfigBar.Message = "Хочешь применить его прямо сейчас?";
                BestConfigBar.IsOpen  = true;
                ApplyBestButton.Tag   = name;
            });
        }

        private void ApplyBest_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not string name) return;
            var bat = Path.Combine(ZapretPaths.StrategiesDir, name);
            if (!File.Exists(bat)) return;
            var ctrl = new WinwsController(ZapretPaths.AppDir);
            ctrl.StopAll();
            ctrl.StartConfig(bat);
            AppendLog($"[ТЕСТ] Применена стратегия: {name}", _colOk);
            BestConfigBar.IsOpen = false;
        }

        // ═════════════════════════════════════════════════════════════════════
        // RICH TEXT LOG
        // ═════════════════════════════════════════════════════════════════════

        private void AppendLog(string msg)
        {
            Color color;
            if (msg.StartsWith("[ОШИБКА]") || msg.Contains("ERROR") || msg.Contains("✗"))
                color = _colError;
            else if (msg.StartsWith("[ПРЕДУПРЕЖДЕНИЕ]") || msg.Contains("WARN") || msg.Contains("⚠"))
                color = _colWarn;
            else if (msg.StartsWith("★"))
                color = Color.FromArgb(255, 255, 205, 60);
            else if (msg.TrimStart().StartsWith("  ") && !msg.StartsWith("["))
                color = _colHeading;
            else if (msg.StartsWith("[INFO]") || msg.StartsWith("[DPI]") || msg.StartsWith("[SVC]"))
                color = _colDim;
            else if (msg.Contains("✓"))
                color = _colOk;
            else
                color = _colDim;

            AppendLog(msg, color);
        }

        private void AppendLog(string text, Color? color = null,
            bool bold = false, double size = 0)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var para = new Paragraph { Margin = new Thickness(0) };
                var run  = new Run { Text = text };
                run.Foreground = new SolidColorBrush(color ?? _colDim);
                if (bold) run.FontWeight = FontWeights.SemiBold;
                if (size > 0) run.FontSize = size;
                para.Inlines.Add(run);
                LogRichText.Blocks.Add(para);
                LogScrollViewer.ChangeView(null, double.MaxValue, null);
            });
        }

        private void AppendLogSeparator() =>
            AppendLog("─────────────────────────────────────────", _colDim);

        private void ClearLog() =>
            DispatcherQueue.TryEnqueue(() => LogRichText.Blocks.Clear());

        private string CollectLogText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (Block b in LogRichText.Blocks)
                if (b is Paragraph p)
                    foreach (var inline in p.Inlines)
                        if (inline is Run r)
                            sb.AppendLine(r.Text);
            return sb.ToString();
        }

        // ═════════════════════════════════════════════════════════════════════
        // PROGRESS
        // ═════════════════════════════════════════════════════════════════════

        private void OnTestProgress(TestProgress p)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressConfigText.Text = ShortName(p.ConfigName);
                ProgressPhaseText.Text  = p.Phase;
                ProgressCountText.Text  = $"{p.ConfigIndex} / {p.TotalConfigs}";
                TestProgressBar.Value   = (double)p.ConfigIndex / p.TotalConfigs * 100;
            });
        }

        // ═════════════════════════════════════════════════════════════════════
        // IDLE / RUNNING STATE
        // ═════════════════════════════════════════════════════════════════════

        private void SetRunning()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RunTestButton.IsEnabled   = false;
                StopTestButton.Visibility = Visibility.Visible;
                ProgressCard.Visibility   = Visibility.Visible;
                TestProgressRing.IsActive = true;
                TestProgressBar.Value     = 0;
                ProgressConfigText.Text   = "Подготовка...";
                ProgressPhaseText.Text    = "";
                StrategiesDropDown.IsEnabled = false;
                ModeStandard.IsEnabled    = false;
                ModeDpi.IsEnabled         = false;
            });
        }

        private void SetIdle()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RunTestButton.IsEnabled   = true;
                StopTestButton.Visibility = Visibility.Collapsed;
                ProgressCard.Visibility   = Visibility.Collapsed;
                TestProgressRing.IsActive = false;
                StrategiesDropDown.IsEnabled = true;
                ModeStandard.IsEnabled    = true;
                ModeDpi.IsEnabled         = true;
            });
        }


        private async void SaveTest_Click(object sender, RoutedEventArgs e)
        {
            var resultsText = "";
            if (_lastResults != null && _lastResults.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== Результаты тестирования zapret ===");
                sb.AppendLine($"Дата: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                sb.AppendLine($"Режим: {_currentMode}");
                sb.AppendLine($"Стратегий протестировано: {_lastResults.Count}");
                sb.AppendLine();

                foreach (var result in _lastResults)
                {
                    sb.AppendLine($"=== {result.ConfigName} ===");
                    if (_currentMode == TestMode.Standard)
                    {
                        sb.AppendLine($"HTTP OK: {result.HttpOk}, ERR: {result.HttpErr}, UNSUP: {result.HttpUnsup}");
                        sb.AppendLine($"Ping OK: {result.PingOk}");
                    }
                    else
                    {
                        sb.AppendLine($"DPI OK: {result.DpiOk}, BLOCKED: {result.DpiBlocked}");
                    }
                    sb.AppendLine();
                }

                var best = ZapretTestRunner.FindBest(_lastResults, _currentMode);
                if (best != null)
                {
                    sb.AppendLine("=== Лучший результат ===");
                    sb.AppendLine($"Стратегия: {best.ConfigName}");
                    sb.AppendLine($"Оценка: {(best.Score * 100):F1}%");
                }

                resultsText = sb.ToString();
            }
            else
            {
                resultsText = "Нет результатов тестирования для сохранения.";
            }

            await SaveTextToFile(resultsText, $"zapret_test_results_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
        }

        // ═════════════════════════════════════════════════════════════════════
        // FILE SAVE
        // ═════════════════════════════════════════════════════════════════════

        private static async Task SaveTextToFile(string text, string defaultFileName)
        {
            var picker = new FileSavePicker { SuggestedFileName = defaultFileName };
            picker.FileTypeChoices.Add("Текстовый файл", new List<string> { ".txt" });
            // ↓ ИСПРАВЛЕНО: .Instance → .MainWindow
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
                (Application.Current as App)?.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var file = await picker.PickSaveFileAsync();
            if (file is not null) await FileIO.WriteTextAsync(file, text);
        }
    }
}
