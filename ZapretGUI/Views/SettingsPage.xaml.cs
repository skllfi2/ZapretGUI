using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using ZapretGUI.Services;

namespace ZapretGUI.Views
{
    public sealed partial class SettingsPage : Page
    {
        private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "ZapretGUI";
        private bool _isLoading = true;

        public SettingsPage()
        {
            this.InitializeComponent();
            LoadSettings();
            _isLoading = false;
            _ = Task.Run(LoadServiceStatus);
            UpdateIpsetStatus();
        }

        private void LoadSettings()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
            AutostartToggle.IsOn = key?.GetValue(AppName) != null;

            AutoStartZapretToggle.IsOn = AppSettings.AutoStartZapret;
            MinimizeToTrayToggle.IsOn = AppSettings.MinimizeToTrayOnStart;
            SoundEffectsToggle.IsOn = AppSettings.SoundEffects;
            ToastNotificationsToggle.IsOn = AppSettings.ToastNotifications;
            AutoUpdateCheckToggle.IsOn = AppSettings.AutoUpdateCheck;

            ThemeComboBox.SelectedIndex = AppSettings.Theme switch
            {
                "Light" => 1,
                "Dark" => 2,
                _ => 0
            };

            LanguageComboBox.SelectedIndex = AppSettings.Language == "en" ? 1 : 0;

            // Game Filter
            GameFilterComboBox.SelectedIndex = AppSettings.GameFilter switch
            {
                "all" => 1,
                "tcp" => 2,
                "udp" => 3,
                _ => 0
            };

            // IPSet Filter
            IpsetFilterComboBox.SelectedIndex = AppSettings.IpsetFilter switch
            {
                "loaded" => 1,
                "none" => 2,
                _ => 0
            };
        }

        private void UpdateIpsetStatus()
        {
            var ipsetFile = Path.Combine(ZapretPaths.ListsDir, "ipset-all.txt");
            if (File.Exists(ipsetFile))
            {
                var lines = 0;
                try { lines = File.ReadAllLines(ipsetFile).Length; } catch { }
                IpsetStatusText.Text = $"Загружен список: {lines} записей";
            }
            else
            {
                IpsetStatusText.Text = "Файл ipset-all.txt не найден";
            }
        }

        private void AutostartToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            if (AutostartToggle.IsOn)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                key?.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key?.DeleteValue(AppName, false);
            }
        }

        private void AutoStartZapretToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            AppSettings.AutoStartZapret = AutoStartZapretToggle.IsOn;
            AppSettings.Save();
        }

        private void MinimizeToTrayToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            AppSettings.MinimizeToTrayOnStart = MinimizeToTrayToggle.IsOn;
            AppSettings.Save();
        }

        private void SoundEffectsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            AppSettings.SoundEffects = SoundEffectsToggle.IsOn;
            AppSettings.Save();
        }

        private void ToastNotificationsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            AppSettings.ToastNotifications = ToastNotificationsToggle.IsOn;
            AppSettings.Save();
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            var tag = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Default";
            AppSettings.Theme = tag;
            AppSettings.Save();
            ApplyTheme(tag);
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            var tag = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ru";
            if (tag == AppSettings.Language) return;
            AppSettings.Language = tag;
            AppSettings.Save();
            MainWindow.Instance?.ReloadAllPages();
        }

        private void GameFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            var tag = (GameFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "disabled";
            AppSettings.GameFilter = tag;
            AppSettings.Save();
        }

        private void IpsetFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            var tag = (IpsetFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "any";
            AppSettings.IpsetFilter = tag;
            AppSettings.Save();
        }

        private static void ApplyTheme(string theme)
        {
            if (MainWindow.Instance?.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
            }
        }

        private async void LoadServiceStatus()
        {
            var status = await ServiceManager.GetStatusAsync();
            DispatcherQueue.TryEnqueue(() => ServiceStatusText.Text = status);
        }

        private void AppendServiceLog(string text)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ServiceLogText.Text += text + "\n";
                ServiceLogScrollViewer.ScrollToVerticalOffset(double.MaxValue);
            });
        }

        private async void InstallService_Click(object sender, RoutedEventArgs e)
        {
            var strategy = AppState.CurrentStrategy;
            if (string.IsNullOrEmpty(strategy))
            {
                AppendServiceLog("Ошибка: сначала выберите стратегию на странице Стратегии");
                return;
            }

            var batFile = Path.Combine(ZapretPaths.StrategiesDir, strategy + ".bat");
            var arguments = BatStrategyParser.ParseStrategy(batFile);

            if (arguments == null)
            {
                AppendServiceLog("Ошибка: не удалось распарсить стратегию");
                return;
            }

            InstallServiceButton.IsEnabled = false;
            AppendServiceLog($"Устанавливаю службу со стратегией: {strategy}...");
            await ServiceManager.InstallAsync(strategy, arguments, AppendServiceLog);
            InstallServiceButton.IsEnabled = true;
            _ = Task.Run(LoadServiceStatus);
        }

        private async void RemoveService_Click(object sender, RoutedEventArgs e)
        {
            AppendServiceLog("Удаляю службы...");
            await ServiceManager.RemoveAsync(AppendServiceLog);
            AppendServiceLog("Готово.");
            _ = Task.Run(LoadServiceStatus);
        }

        private void AutoUpdateCheckToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            AppSettings.AutoUpdateCheck = AutoUpdateCheckToggle.IsOn;
            AppSettings.Save();
        }
    }
}
