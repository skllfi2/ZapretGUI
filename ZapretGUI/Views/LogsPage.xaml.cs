using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.UI;

namespace ZapretGUI.Views
{
    public sealed partial class LogsPage : Page
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        // Ресурсы для проверки: название, URL
        private static readonly (string Name, string Url)[] TestTargets =
        [
            ("Discord", "https://discord.com"),
            ("YouTube", "https://youtube.com"),
            ("Google", "https://google.com"),
        ];

        public LogsPage()
        {
            this.InitializeComponent();

            foreach (var line in AppState.Logs)
                LogsTextBlock.Text += line + "\n";

            AppState.WinwsService.LogReceived += OnLogReceived;
        }

        private void OnLogReceived(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LogsTextBlock.Text += message + "\n";
                LogsScrollViewer.ChangeView(null, LogsScrollViewer.ScrollableHeight, null);
            });
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            AppState.Logs.Clear();
            LogsTextBlock.Text = string.Empty;
        }

        private async void RunTest_Click(object sender, RoutedEventArgs e)
        {
            RunTestButton.IsEnabled = false;
            TestProgressRing.IsActive = true;
            TestStatusText.Text = "Тестирование...";

            AppendLog("─────────────────────────────────");
            AppendLog($"[ТЕСТ] Запуск проверки подключения ({DateTime.Now:HH:mm:ss})");
            AppendLog($"[ТЕСТ] Защита: {(AppState.WinwsService.IsRunning ? "АКТИВНА" : "ОСТАНОВЛЕНА")}");
            AppendLog("─────────────────────────────────");

            // Сначала пробуем запустить test_zapret.ps1 если есть
            var psScript = System.IO.Path.Combine(ZapretPaths.UtilsDir, "test_zapret.ps1");
            var psScript2 = System.IO.Path.Combine(ZapretPaths.UtilsDir, "test zapret.ps1");

            if (System.IO.File.Exists(psScript) || System.IO.File.Exists(psScript2))
            {
                var script = System.IO.File.Exists(psScript) ? psScript : psScript2;
                AppendLog($"[ТЕСТ] Найден скрипт: {System.IO.Path.GetFileName(script)}");
                await RunPowerShellTestAsync(script);
            }
            else
            {
                AppendLog("[ТЕСТ] Скрипт test_zapret.ps1 не найден — запуск встроенной проверки");
                await RunBuiltinTestAsync();
            }

            AppendLog("─────────────────────────────────");

            TestProgressRing.IsActive = false;
            RunTestButton.IsEnabled = true;
            TestStatusText.Text = "Готово";
            LogsScrollViewer.ChangeView(null, LogsScrollViewer.ScrollableHeight, null);
        }

        private async Task RunPowerShellTestAsync(string scriptPath)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) { AppendLog("[ТЕСТ] Не удалось запустить PowerShell"); return; }

                proc.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLog("[PS] " + e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog("[PS ERR] " + e.Data); };
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                await proc.WaitForExitAsync();
                AppendLog($"[ТЕСТ] Скрипт завершён (код: {proc.ExitCode})");
            }
            catch (Exception ex)
            {
                AppendLog($"[ТЕСТ] Ошибка запуска скрипта: {ex.Message}");
                AppendLog("[ТЕСТ] Запуск встроенной проверки...");
                await RunBuiltinTestAsync();
            }
        }

        private async Task RunBuiltinTestAsync()
        {
            var results = new List<(string Name, bool Ok, long Ms)>();

            foreach (var (name, url) in TestTargets)
            {
                AppendLog($"[ТЕСТ] Проверка {name} ({url})...");
                var (ok, ms) = await CheckUrlAsync(url);
                results.Add((name, ok, ms));
                var status = ok ? $"✓ OK ({ms} мс)" : "✗ НЕДОСТУПЕН";
                AppendLog($"[ТЕСТ] {name}: {status}");
            }

            AppendLog("");
            AppendLog("[ТЕСТ] Итог:");
            var passed = 0;
            foreach (var (name, ok, ms) in results)
            {
                AppendLog($"  {(ok ? "✓" : "✗")} {name}");
                if (ok) passed++;
            }
            AppendLog($"[ТЕСТ] Прошло: {passed}/{results.Count}");

            DispatcherQueue.TryEnqueue(() =>
            {
                TestStatusText.Text = $"Прошло: {passed}/{results.Count}";
            });
        }

        private static async Task<(bool Ok, long Ms)> CheckUrlAsync(string url)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                sw.Stop();
                return (resp.IsSuccessStatusCode || (int)resp.StatusCode < 500, sw.ElapsedMilliseconds);
            }
            catch
            {
                return (false, 0);
            }
        }

        private void AppendLog(string text)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LogsTextBlock.Text += text + "\n";
                AppState.Logs.Add(text);
            });
        }
    }
}
