using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using ZUI.Services;

namespace ZUI.Views
{
    public sealed partial class ServicesPage : Page
    {
        // ── Константы обновлений ──────────────────────────────────────
        private const string VersionUrl     = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/main/.service/version.txt";
        private const string IpsetUrl       = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/ipset-service.txt";
        private const string HostsUrl       = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/hosts";
        private const string MalwHostsUrl   = "https://raw.githubusercontent.com/ImMALWARE/dns.malw.link/master/hosts";
        private const string FlowsealApiUrl = "https://api.github.com/repos/Flowseal/zapret-discord-youtube/commits?path=.service/hosts&per_page=1";

        private static bool IsRunningAsAdmin()
        {
            var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(id)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        private static void RestartAsAdmin()
        {
            var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe, UseShellExecute = true, Verb = "runas"
            });
            Application.Current.Exit();
        }

        private static string ComputeSHA256(string text)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(bytes);
        }

        private static async Task<string> BuildMergedHostsBlock(HttpClient http)
        {
            var flowsealContent = await http.GetStringAsync(
                "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/hosts");
            var malwContent = await http.GetStringAsync(
                "https://raw.githubusercontent.com/ImMALWARE/dns.malw.link/master/hosts");

            var flowsealDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in flowsealContent.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith('#') || string.IsNullOrWhiteSpace(t)) continue;
                var parts = t.Split(new char[]{' ','\t'}, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2) flowsealDomains.Add(parts[1]);
            }

            var extraLines = new List<string>();
            foreach (var line in malwContent.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith('#') || string.IsNullOrWhiteSpace(t)) continue;
                var parts = t.Split(new char[]{' ','\t'}, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && !flowsealDomains.Contains(parts[1]))
                    extraLines.Add(t);
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# ===== zapret hosts (Flowseal) =====");
            sb.Append(flowsealContent.TrimEnd());
            if (extraLines.Count > 0)
            {
                sb.AppendLine(); sb.AppendLine();
                sb.AppendLine("# ===== дополнительные домены (ImMALWARE/dns.malw.link) =====");
                foreach (var l in extraLines) sb.AppendLine(l);
            }
            return sb.ToString();
        }

        public async Task CheckHostsUpdateAvailable()
        {
            try
            {
                // Лёгкий запрос к GitHub API — только дата последнего коммита
                var req = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get, FlowsealApiUrl);
                req.Headers.Add("User-Agent", "Z-UI");
                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return;

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var dateStr = doc.RootElement[0]
                    .GetProperty("commit").GetProperty("author").GetProperty("date").GetString();
                if (DateTime.TryParse(dateStr, out var commitDate) && commitDate > AppSettings.HostsLastCheck)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        HostsUpdateBadge.Visibility = Visibility.Visible;
                        HostsStatusText.Text = "Доступно обновление hosts";
                    });

                    // Автообновление если включено и есть права
                    if (AppSettings.HostsAutoUpdate && IsRunningAsAdmin())
                        await ApplyHostsAsync(silent: true);
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                        HostsLastCheckText.Text = $"Проверено: {AppSettings.HostsLastCheck:dd.MM.yy HH:mm}");
                }
                AppSettings.HostsLastCheck = DateTime.UtcNow;
                AppSettings.Save();
            }
            catch { }
        }

        private async Task ApplyHostsAsync(bool silent = false)
        {
            var hostsFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "drivers", "etc", "hosts");
            try
            {
                if (!silent)
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        HostsProgressPanel.Visibility = Visibility.Visible;
                        HostsProgressBar.IsIndeterminate = true;
                        HostsProgressLabel.Text = "↓ Скачиваю Flowseal hosts...";
                        HostsStatusText.Text = "Загружается...";
                        UpdateHostsButton.IsEnabled = false;
                    });

                var block = await BuildMergedHostsBlock(_http);
                var hash  = ComputeSHA256(block);

                if (!silent)
                    DispatcherQueue.TryEnqueue(() => HostsProgressLabel.Text = "Проверяю актуальность...");

                if (hash == AppSettings.HostsHash)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        HostsStatusText.Text = "Hosts актуален";
                        HostsLastCheckText.Text = $"Обновлён: {AppSettings.HostsLastCheck:dd.MM.yy HH:mm}";
                        HostsUpdateBadge.Visibility = Visibility.Collapsed;
                        HostsProgressPanel.Visibility = Visibility.Collapsed;
                        UpdateHostsButton.IsEnabled = true;
                    });
                    if (!silent) AppendUpdateLog("✓ Файл hosts актуален");
                    return;
                }

                if (!silent)
                    DispatcherQueue.TryEnqueue(() => HostsProgressLabel.Text = "Применяю изменения...");

                var localContent = await File.ReadAllTextAsync(hostsFile);
                var backupFile   = hostsFile + ".backup_zui";
                if (!File.Exists(backupFile)) File.Copy(hostsFile, backupFile);

                var clean = localContent;
                var idx   = clean.IndexOf("# ===== zapret hosts");
                if (idx >= 0) clean = clean[..idx].TrimEnd();

                await File.WriteAllTextAsync(hostsFile, clean + Environment.NewLine + Environment.NewLine + block);

                AppSettings.HostsHash        = hash;
                AppSettings.HostsLastCheck   = DateTime.UtcNow;
                AppSettings.Save();

                var count = block.Split('\n').Length;
                DispatcherQueue.TryEnqueue(() =>
                {
                    HostsStatusText.Text = "Hosts обновлён ✓";
                    HostsLastCheckText.Text = $"Обновлён: {DateTime.Now:dd.MM.yy HH:mm}";
                    HostsUpdateBadge.Visibility = Visibility.Collapsed;
                    HostsProgressPanel.Visibility = Visibility.Collapsed;
                    UpdateHostsButton.IsEnabled = true;
                });
                if (!silent) AppendUpdateLog($"✓ Hosts применён ({count} строк). Бэкап: {backupFile}");
            }
            catch (UnauthorizedAccessException)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    HostsStatusText.Text = "Нет прав администратора";
                    HostsProgressPanel.Visibility = Visibility.Collapsed;
                    UpdateHostsButton.IsEnabled = true;
                });
                if (!silent) AppendUpdateLog("✗ Нет прав на запись hosts");
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    HostsStatusText.Text = "Ошибка";
                    HostsProgressPanel.Visibility = Visibility.Collapsed;
                    UpdateHostsButton.IsEnabled = true;
                });
                if (!silent) AppendUpdateLog($"✗ Ошибка: {ex.Message}");
            }
        }
        private const string ApiReleasesUrl = "https://api.github.com/repos/Flowseal/zapret-discord-youtube/releases/latest";

        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(60),
            DefaultRequestHeaders = { { "User-Agent", "ZUI" } }
        };

        // ── Тестирование ──────────────────────────────────────────────

        // ─────────────────────────────────────────────────────────────

        public ServicesPage()
        {
            this.InitializeComponent();

            InitTestingTab();

            // Перенос строк по тогглу
            LogsWrapButton.Checked   += (_, _) => LogsTextBox.TextWrapping = TextWrapping.Wrap;
            LogsWrapButton.Unchecked += (_, _) => LogsTextBox.TextWrapping = TextWrapping.NoWrap;

            // Кнопка копирования
            LogsCopyButton.Click += CopyLogs_Click;

            foreach (var line in AppState.Logs)
                LogsTextBox.Text += line + "\n";
            AppState.WinwsService.LogReceived += OnLogReceived;

            // Начальный счётчик строк
            var initLines = LogsTextBox.Text.Split('\n').Length - 1;
            LogsLineCountText.Text = $"{initLines} строк";

            // Диагностика запускается при переключении на вкладку 2 (не изменилась)
            // Обновления инициализируются при переключении на вкладку 0
            MainPivot.SelectionChanged += async (s, e) =>
            {
                if (MainPivot.SelectedIndex == 2) _ = RunDiagnosticsAsync();
                if (MainPivot.SelectedIndex == 0) await CheckVersionSilentAsync();
            };

            // Бейдж на вкладке Обновления
            if (UpdateChecker.UpdateAvailable)
                UpdatesTabBadge.Visibility = Microsoft.UI.Xaml.Visibility.Visible;

            UpdateChecker.UpdateFound += _ =>
            {
                DispatcherQueue.TryEnqueue(() =>
                    UpdatesTabBadge.Visibility = Microsoft.UI.Xaml.Visibility.Visible);
            };
        }

        // При навигации сюда с параметром — переключаем вкладку
        // NavigateTo("updates") передаёт параметр 0
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is int tabIndex)
                DispatcherQueue.TryEnqueue(() => MainPivot.SelectedIndex = tabIndex);
        }

        // ══════════════════════════════════════════════════════════════
        // ЛОГИ
        // ══════════════════════════════════════════════════════════════

        private void OnLogReceived(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LogsTextBox.Text += message + "\n";

                // Обновляем счётчик строк
                var lines = LogsTextBox.Text.Split('\n').Length - 1;
                LogsLineCountText.Text = $"{lines} строк";

                // Авто-прокрутка только если включена
                if (LogsAutoScrollButton.IsChecked == true)
                    LogsScrollViewer.ChangeView(null, LogsScrollViewer.ScrollableHeight, null);
            });
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            AppState.Logs.Clear();
            LogsTextBox.Text = string.Empty;
            LogsLineCountText.Text = "0 строк";
        }

        private async void SaveLogs_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"zapret_logs_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"
            };
            picker.FileTypeChoices.Add("\u0422\u0435\u043a\u0441товый \u0444айл", new List<string> { ".txt" });

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(Window.Current);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
                await FileIO.WriteTextAsync(file, LogsTextBox.Text);
        }

        private void CopyLogs_Click(object sender, RoutedEventArgs e)
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(LogsTextBox.Text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }


        // ══════════════════════════════════════════════════════════════
        // ДИАГНОСТИКА
        // ══════════════════════════════════════════════════════════════

        private record CheckResult(string Name, bool Ok, string Detail);

        private void RunDiagnostics_Click(object sender, RoutedEventArgs e) => _ = RunDiagnosticsAsync();

        private async Task RunDiagnosticsAsync()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ChecksList.Children.Clear();
                SummaryBar.IsOpen     = false;
                DiagSubtitleText.Text = "Выполняется проверка...";
                DiagPassCount.Text    = "0";
                DiagFailCount.Text    = "0";
                ChecksList.Children.Add(new ProgressRing { IsActive = true, Width = 32, Height = 32, Margin = new Thickness(16) });
            });

            var results = await Task.Run(RunAllChecks);

            DispatcherQueue.TryEnqueue(() =>
            {
                ChecksList.Children.Clear();
                int passed = 0;
                foreach (var r in results) { if (r.Ok) passed++; ChecksList.Children.Add(BuildCheckRow(r)); }

                int failed = results.Count - passed;
                DiagPassCount.Text    = passed.ToString();
                DiagFailCount.Text    = failed.ToString();
                DiagSubtitleText.Text = passed == results.Count
                    ? $"Все проверки пройдены · {results.Count} компонентов"
                    : $"{failed} проблем из {results.Count}";

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
            var r = new List<CheckResult>();
            FileCheck(r, "winws.exe",       ZapretPaths.WinwsExe);
            FileCheck(r, "WinDivert.dll",   Path.Combine(ZapretPaths.WinwsDir, "WinDivert.dll"));
            FileCheck(r, "WinDivert64.sys", Path.Combine(ZapretPaths.WinwsDir, "WinDivert64.sys"));
            FileCheck(r, "cygwin1.dll",     Path.Combine(ZapretPaths.WinwsDir, "cygwin1.dll"));

            var lg = Path.Combine(ZapretPaths.ListsDir, "list-general.txt");
            r.Add(new CheckResult("list-general.txt", File.Exists(lg),
                File.Exists(lg) ? $"{CountLines(lg)} доменов" : $"Не найден: {lg}"));

            var ip = Path.Combine(ZapretPaths.ListsDir, "ipset-all.txt");
            r.Add(new CheckResult("ipset-all.txt", File.Exists(ip),
                File.Exists(ip) ? $"{CountLines(ip)} записей" : $"Не найден: {ip}"));

            FileCheck(r, "list-exclude.txt", Path.Combine(ZapretPaths.ListsDir, "list-exclude.txt"));

            var bats = Directory.Exists(ZapretPaths.StrategiesDir)
                ? Directory.GetFiles(ZapretPaths.StrategiesDir, "*.bat")
                : Array.Empty<string>();
            r.Add(new CheckResult("Стратегии (.bat)", bats.Length > 0,
                bats.Length > 0 ? $"{bats.Length} стратегий найдено" : $"Не найдены: {ZapretPaths.StrategiesDir}"));

            var vOk = File.Exists(ZapretPaths.VersionFile);
            r.Add(new CheckResult("version.txt", vOk,
                vOk ? $"Версия zapret: {File.ReadAllText(ZapretPaths.VersionFile).Trim()}" : $"Не найден: {ZapretPaths.VersionFile}"));

            bool isAdmin = false;
            try
            {
                var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                isAdmin = new System.Security.Principal.WindowsPrincipal(id)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { }
            r.Add(new CheckResult("Права администратора", isAdmin,
                isAdmin ? "Запущено с правами администратора" : "Требуются права администратора"));

            return r;
        }

        private static void FileCheck(List<CheckResult> list, string name, string path) =>
            list.Add(new CheckResult(name, File.Exists(path),
                File.Exists(path) ? path : $"Не найден: {path}"));

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
                Padding      = new Thickness(16, 12, 16, 12),
                Background   = new SolidColorBrush(result.Ok
                    ? Color.FromArgb(15, 0, 200, 0)
                    : Color.FromArgb(15, 200, 0, 0))
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new FontIcon
            {
                Glyph = result.Ok ? "\uE73E" : "\uE783", FontSize = 16,
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
                Text = result.Detail, Opacity = 0.6,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
                MaxWidth = 380, TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(detail, 2);

            grid.Children.Add(icon); grid.Children.Add(name); grid.Children.Add(detail);
            border.Child = grid;
            ToolTipService.SetToolTip(border, result.Detail);
            return border;
        }

        // ══════════════════════════════════════════════════════════════
        // ОБНОВЛЕНИЯ
        // ══════════════════════════════════════════════════════════════

        private void AppendUpdateLog(string text)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateLogText.Text += text + "\n";
                UpdateLogScrollViewer.ScrollToVerticalOffset(double.MaxValue);
            });
        }

        private void SetUpdateButtonsEnabled(bool enabled)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                CheckVersionButton.IsEnabled  = enabled;
                UpdateIpsetButton.IsEnabled   = enabled;
                UpdateHostsButton.IsEnabled   = enabled;
            });
        }

        private async Task CheckVersionSilentAsync()
        {
            try
            {
                var latest = (await _http.GetStringAsync(VersionUrl)).Trim();
                var local  = ZapretPaths.LocalVersion;
                DispatcherQueue.TryEnqueue(() =>
                    VersionStatusText.Text = local == latest
                        ? $"Установлена актуальная версия: {local}"
                        : $"Доступна новая версия: {latest} (установлена: {local})");
            }
            catch
            {
                DispatcherQueue.TryEnqueue(() =>
                    VersionStatusText.Text = $"Версия: {ZapretPaths.LocalVersion} (нет подключения)");
            }
        }

        private async void CheckVersion_Click(object sender, RoutedEventArgs e)
        {
            SetUpdateButtonsEnabled(false);
            DispatcherQueue.TryEnqueue(() => VersionStatusText.Text = "Проверяется...");
            AppendUpdateLog("Проверяю версию...");

            try
            {
                var latest = (await _http.GetStringAsync(VersionUrl)).Trim();
                var local  = ZapretPaths.LocalVersion;

                if (local == latest)
                {
                    DispatcherQueue.TryEnqueue(() => VersionStatusText.Text = $"Установлена актуальная версия: {local}");
                    AppendUpdateLog($"✓ Версия актуальна: {local}");
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                        VersionStatusText.Text = $"Доступна новая версия: {latest} (установлена: {local})");
                    AppendUpdateLog($"↑ Доступна новая версия: {latest}");

                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        var dialog = new ContentDialog
                        {
                            Title             = "Доступно обновление",
                            Content           = $"Новая версия: {latest}\nУстановленная: {local}\n\nСкачать и установить?",
                            PrimaryButtonText = "Обновить",
                            CloseButtonText   = "Позже",
                            XamlRoot          = this.XamlRoot
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                            await DownloadAndInstallUpdateAsync(latest);
                    });
                }
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() => VersionStatusText.Text = "Ошибка проверки версии");
                AppendUpdateLog($"✗ Ошибка: {ex.Message}");
            }

            SetUpdateButtonsEnabled(true);
        }

        private async Task DownloadAndInstallUpdateAsync(string version)
        {
            SetUpdateButtonsEnabled(false);
            AppendUpdateLog($"\n--- Обновление до версии {version} ---");

            try
            {
                AppendUpdateLog("Получаю информацию о релизе...");
                var json = await _http.GetStringAsync(ApiReleasesUrl);
                using var doc = JsonDocument.Parse(json);
                var assets = doc.RootElement.GetProperty("assets");

                string? downloadUrl = null, assetName = null;
                foreach (var asset in assets.EnumerateArray())
                {
                    var n = asset.GetProperty("name").GetString() ?? "";
                    if (n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        assetName = n;
                        break;
                    }
                }

                if (downloadUrl == null) { AppendUpdateLog("✗ Архив не найден в релизе"); SetUpdateButtonsEnabled(true); return; }
                AppendUpdateLog($"Архив: {assetName}");

                var tempZip = Path.Combine(Path.GetTempPath(), $"zapret_{version}.zip");
                AppendUpdateLog("Скачиваю...");

                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateProgressCard.Visibility  = Visibility.Visible;
                    UpdateProgressBar.Value        = 0;
                    UpdateProgressLabel.Text       = "Скачивание обновления...";
                    UpdateProgressPct.Text         = "0%";
                    UpdateProgressDetail.Text      = "";
                });

                using (var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    var total = response.Content.Headers.ContentLength ?? 0;
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var file   = File.Create(tempZip);
                    var buffer = new byte[65536];
                    long downloaded = 0; int lastPct = -1, read;
                    while ((read = await stream.ReadAsync(buffer)) > 0)
                    {
                        await file.WriteAsync(buffer.AsMemory(0, read));
                        downloaded += read;
                        if (total > 0)
                        {
                            var pct = (int)(downloaded * 100 / total);
                            if (pct != lastPct)
                            {
                                lastPct = pct;
                                var mb      = downloaded / 1024.0 / 1024.0;
                                var totalMb = total / 1024.0 / 1024.0;
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    UpdateProgressBar.Value   = pct;
                                    UpdateProgressPct.Text    = $"{pct}%";
                                    UpdateProgressDetail.Text = $"{mb:F1} МБ / {totalMb:F1} МБ";
                                    VersionStatusText.Text    = $"Скачивание: {pct}%";
                                });
                            }
                        }
                    }
                }

                AppendUpdateLog("✓ Архив скачан");
                AppendUpdateLog("Распаковываю...");
                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateProgressLabel.Text  = "Распаковка и установка...";
                    UpdateProgressBar.Value   = 100;
                    UpdateProgressDetail.Text = "Пожалуйста, подождите...";
                });
                var tempExtract = Path.Combine(Path.GetTempPath(), $"zapret_update_{version}");
                if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                ZipFile.ExtractToDirectory(tempZip, tempExtract);
                AppendUpdateLog("✓ Распаковано");

                var extractedDirs = Directory.GetDirectories(tempExtract);
                var sourceDir = extractedDirs.Length == 1 ? extractedDirs[0] : tempExtract;

                AppState.WinwsService.Stop();
                await Task.Delay(800);

                AppendUpdateLog("Устанавливаю файлы...");
                await CopyUpdateFilesAsync(sourceDir);
                await File.WriteAllTextAsync(ZapretPaths.VersionFile, version);

                File.Delete(tempZip);
                Directory.Delete(tempExtract, true);

                DispatcherQueue.TryEnqueue(() =>
                {
                    VersionStatusText.Text    = $"✓ Обновлено до версии {version}";
                    UpdateProgressLabel.Text  = $"✓ Обновлено до версии {version}";
                    UpdateProgressDetail.Text = "Установка завершена";
                });
                AppendUpdateLog($"\n✓ Zapret успешно обновлён до версии {version}");
            }
            catch (Exception ex)
            {
                AppendUpdateLog($"✗ Ошибка обновления: {ex.Message}");
                DispatcherQueue.TryEnqueue(() =>
                {
                    VersionStatusText.Text    = "Ошибка обновления";
                    UpdateProgressLabel.Text  = "✗ Ошибка обновления";
                    UpdateProgressDetail.Text = ex.Message;
                });
            }

            SetUpdateButtonsEnabled(true);
        }

        private async Task CopyUpdateFilesAsync(string sourceDir)
        {
            var userFiles = new[] { "ipset-exclude-user.txt", "list-general-user.txt", "list-exclude-user.txt" };
            await Task.Run(() =>
            {
                var srcBin = Path.Combine(sourceDir, "bin");
                if (Directory.Exists(srcBin))
                {
                    Directory.CreateDirectory(ZapretPaths.WinwsDir);
                    foreach (var f in Directory.GetFiles(srcBin))
                        File.Copy(f, Path.Combine(ZapretPaths.WinwsDir, Path.GetFileName(f)), true);
                    AppendUpdateLog("  winws бинарники обновлены");
                }
                Directory.CreateDirectory(ZapretPaths.StrategiesDir);
                foreach (var f in Directory.GetFiles(sourceDir, "*.bat"))
                {
                    if (Path.GetFileName(f).StartsWith("service", StringComparison.OrdinalIgnoreCase)) continue;
                    File.Copy(f, Path.Combine(ZapretPaths.StrategiesDir, Path.GetFileName(f)), true);
                }
                AppendUpdateLog("  стратегии обновлены");

                var srcLists = Path.Combine(sourceDir, "lists");
                if (Directory.Exists(srcLists))
                {
                    Directory.CreateDirectory(ZapretPaths.ListsDir);
                    foreach (var f in Directory.GetFiles(srcLists))
                    {
                        var fname = Path.GetFileName(f);
                        if (Array.Exists(userFiles, u => u.Equals(fname, StringComparison.OrdinalIgnoreCase)))
                        { AppendUpdateLog($"  пропущен: {fname} (пользовательский)"); continue; }
                        File.Copy(f, Path.Combine(ZapretPaths.ListsDir, fname), true);
                    }
                    AppendUpdateLog("  списки обновлены");
                }

                var srcUtils = Path.Combine(sourceDir, "utils");
                if (Directory.Exists(srcUtils))
                {
                    Directory.CreateDirectory(ZapretPaths.UtilsDir);
                    foreach (var f in Directory.GetFiles(srcUtils))
                        File.Copy(f, Path.Combine(ZapretPaths.UtilsDir, Path.GetFileName(f)), true);
                    AppendUpdateLog("  utils обновлены");
                }
            });
        }

        private async void UpdateIpset_Click(object sender, RoutedEventArgs e)
        {
            SetUpdateButtonsEnabled(false);
            DispatcherQueue.TryEnqueue(() => IpsetStatusText.Text = "Загружается...");
            AppendUpdateLog("\nОбновляю ipset-all.txt...");
            try
            {
                var content  = await _http.GetStringAsync(IpsetUrl);
                var listFile = Path.Combine(ZapretPaths.ListsDir, "ipset-all.txt");
                Directory.CreateDirectory(ZapretPaths.ListsDir);
                await File.WriteAllTextAsync(listFile, content);
                var lines = content.Split('\n').Length;
                DispatcherQueue.TryEnqueue(() => IpsetStatusText.Text = $"Обновлено: {lines} записей");
                AppendUpdateLog($"✓ ipset-all.txt обновлён ({lines} записей)");
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() => IpsetStatusText.Text = "Ошибка обновления");
                AppendUpdateLog($"✗ Ошибка: {ex.Message}");
            }
            SetUpdateButtonsEnabled(true);
        }

        private async void UpdateHosts_Click(object sender, RoutedEventArgs e)
        {
            if (!IsRunningAsAdmin())
            {
                var dialog = new ContentDialog
                {
                    Title             = "Требуются права администратора",
                    Content           = "Запись в hosts требует прав администратора.\nПерезапустить Z-UI от имени администратора?",
                    PrimaryButtonText = "Перезапустить",
                    CloseButtonText   = "Отмена",
                    XamlRoot          = this.XamlRoot
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    RestartAsAdmin();
                return;
            }

            SetUpdateButtonsEnabled(false);
            AppendUpdateLog("\nОбновление hosts...");
            await ApplyHostsAsync(silent: false);
            SetUpdateButtonsEnabled(true);
        }
    }
}
