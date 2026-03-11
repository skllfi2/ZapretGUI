using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using ZUI.Services;

namespace ZUI.Views
{
    public sealed partial class DiagnosticsPage : Page
    {
        private record CheckResult(string Name, bool Ok, string Detail);

        public DiagnosticsPage()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => _ = RunChecksAsync();
        }

        private void RunChecks_Click(object sender, RoutedEventArgs e)
        {
            _ = RunChecksAsync();
        }

        private async Task RunChecksAsync()
        {
            // Всё UI — на UI потоке через DispatcherQueue
            DispatcherQueue.TryEnqueue(() =>
            {
                ChecksList.Children.Clear();
                SummaryBar.IsOpen = false;
                ChecksList.Children.Add(new ProgressRing
                {
                    IsActive = true,
                    Width = 32,
                    Height = 32,
                    Margin = new Thickness(16)
                });
            });

            // Проверки — в фоновом потоке
            var results = await Task.Run(RunAllChecks);

            // Результаты — обратно на UI поток
            DispatcherQueue.TryEnqueue(() =>
            {
                ChecksList.Children.Clear();

                int passed = 0;
                foreach (var r in results)
                {
                    if (r.Ok) passed++;
                    ChecksList.Children.Add(BuildCheckRow(r));
                }

                SummaryBar.IsOpen = true;
                if (passed == results.Count)
                {
                    SummaryBar.Severity = InfoBarSeverity.Success;
                    SummaryBar.Title = $"Все проверки пройдены ({passed}/{results.Count})";
                }
                else
                {
                    SummaryBar.Severity = InfoBarSeverity.Warning;
                    SummaryBar.Title = $"Пройдено {passed} из {results.Count} проверок";
                }
            });
        }

        private static List<CheckResult> RunAllChecks()
        {
            var results = new List<CheckResult>();

            // 1. winws.exe
            var winwsExe = ZapretPaths.WinwsExe;
            results.Add(new CheckResult(
                "winws.exe",
                File.Exists(winwsExe),
                File.Exists(winwsExe) ? winwsExe : $"Не найден: {winwsExe}"));

            // 2. WinDivert.dll
            var windivertDll = Path.Combine(ZapretPaths.WinwsDir, "WinDivert.dll");
            results.Add(new CheckResult(
                "WinDivert.dll",
                File.Exists(windivertDll),
                File.Exists(windivertDll) ? windivertDll : $"Не найден: {windivertDll}"));

            // 3. WinDivert64.sys
            var windivertSys = Path.Combine(ZapretPaths.WinwsDir, "WinDivert64.sys");
            results.Add(new CheckResult(
                "WinDivert64.sys",
                File.Exists(windivertSys),
                File.Exists(windivertSys) ? windivertSys : $"Не найден: {windivertSys}"));

            // 4. cygwin1.dll
            var cygwin = Path.Combine(ZapretPaths.WinwsDir, "cygwin1.dll");
            results.Add(new CheckResult(
                "cygwin1.dll",
                File.Exists(cygwin),
                File.Exists(cygwin) ? cygwin : $"Не найден: {cygwin}"));

            // 5. list-general.txt
            var listGeneral = Path.Combine(ZapretPaths.ListsDir, "list-general.txt");
            var listGeneralOk = File.Exists(listGeneral);
            var listGeneralLines = listGeneralOk ? CountLines(listGeneral) : 0;
            results.Add(new CheckResult(
                "list-general.txt",
                listGeneralOk,
                listGeneralOk ? $"{listGeneralLines} доменов" : $"Не найден: {listGeneral}"));

            // 6. ipset-all.txt
            var ipset = Path.Combine(ZapretPaths.ListsDir, "ipset-all.txt");
            var ipsetOk = File.Exists(ipset);
            var ipsetLines = ipsetOk ? CountLines(ipset) : 0;
            results.Add(new CheckResult(
                "ipset-all.txt",
                ipsetOk,
                ipsetOk ? $"{ipsetLines} записей" : $"Не найден: {ipset}"));

            // 7. list-exclude.txt
            var listExclude = Path.Combine(ZapretPaths.ListsDir, "list-exclude.txt");
            results.Add(new CheckResult(
                "list-exclude.txt",
                File.Exists(listExclude),
                File.Exists(listExclude) ? listExclude : $"Не найден: {listExclude}"));

            // 8. Стратегии .bat
            var strategiesDir = ZapretPaths.StrategiesDir;
            var batFiles = Directory.Exists(strategiesDir)
                ? Directory.GetFiles(strategiesDir, "*.bat")
                : Array.Empty<string>();
            results.Add(new CheckResult(
                "Стратегии (.bat)",
                batFiles.Length > 0,
                batFiles.Length > 0 ? $"{batFiles.Length} стратегий найдено" : $"Стратегии не найдены: {strategiesDir}"));

            // 9. version.txt
            var versionFile = ZapretPaths.VersionFile;
            var versionOk = File.Exists(versionFile);
            var versionText = versionOk ? File.ReadAllText(versionFile).Trim() : "";
            results.Add(new CheckResult(
                "version.txt",
                versionOk,
                versionOk ? $"Версия zapret: {versionText}" : $"Не найден: {versionFile}"));

            // 10. Права администратора
            bool isAdmin;
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { isAdmin = false; }
            results.Add(new CheckResult(
                "Права администратора",
                isAdmin,
                isAdmin ? "Запущено с правами администратора" : "Требуются права администратора"));

            return results;
        }

        private static int CountLines(string path)
        {
            try { int c = 0; foreach (var _ in File.ReadLines(path)) c++; return c; }
            catch { return 0; }
        }

        private UIElement BuildCheckRow(CheckResult result)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Background = new SolidColorBrush(result.Ok
                    ? Color.FromArgb(15, 0, 200, 0)
                    : Color.FromArgb(15, 200, 0, 0))
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new FontIcon
            {
                Glyph = result.Ok ? "\uE73E" : "\uE783",
                FontSize = 16,
                Foreground = new SolidColorBrush(result.Ok
                    ? Color.FromArgb(255, 0, 180, 0)
                    : Color.FromArgb(255, 200, 50, 50)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 0);

            var name = new TextBlock
            {
                Text = result.Name,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(name, 1);

            var detail = new TextBlock
            {
                Text = result.Detail,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
                MaxWidth = 380,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(detail, 2);

            grid.Children.Add(icon);
            grid.Children.Add(name);
            grid.Children.Add(detail);
            border.Child = grid;

            ToolTipService.SetToolTip(border, result.Detail);
            return border;
        }
    }
}
