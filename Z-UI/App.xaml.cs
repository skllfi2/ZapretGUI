using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Security;
using Windows.UI.Notifications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ZUI.Services;

namespace ZUI
{
    public partial class App : Application
    {
        private Window? _window;
        private IntPtr _hwnd;
        private TrayIcon? _trayIcon;

        public static TrayIcon? TrayIcon { get; private set; }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                _window = new MainWindow();
                _window.Activate();

                _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
                _window.Closed += OnWindowClosed;

                Microsoft.UI.Xaml.ElementSoundPlayer.State = Microsoft.UI.Xaml.ElementSoundPlayerState.On;
                Microsoft.UI.Xaml.ElementSoundPlayer.SpatialAudioMode = Microsoft.UI.Xaml.ElementSpatialAudioMode.Off;
                AppState.WinwsService.SetDispatcherQueue(_window.DispatcherQueue);

                // Initialize toast notifications
                Services.ToastNotifier.Initialize(_hwnd);

                _trayIcon = new TrayIcon(_hwnd,
                    System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Z-UI.ico"),
                    "Z-UI — Остановлено",
                    onShow: ShowMainWindow,
                    onExit: ExitApp);

                TrayIcon = _trayIcon;

                AppState.WinwsService.StatusChanged += isRunning =>
                {
                    _trayIcon?.UpdateStatus(isRunning);
                    // Show toast notification for service status change
                    if (Services.ToastNotifier.IsEnabled)
                    {
                        Services.ToastNotifier.Show(
                            "Статус сервиса",
                            isRunning ? "Запущен" : "Остановлен",
                            Services.ToastType.Success);
                    }
                };

                if (AppSettings.AutoUpdateCheck)
                {
                    _ = Task.Run(async () => await Services.UpdateChecker.CheckAsync());
                }

                // Try auto-start Zapret service if enabled
                if (AppSettings.AutoStartZapret)
                {
                    _ = TryAutoStartZapretAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CRASH: " + ex.ToString());
                throw;
            }
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            args.Handled = true;
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Hide();
        }

        private void ShowMainWindow()
        {
            if (_window == null) return;
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                appWindow.Show();
                _window.Activate();
            });
        }

        private async Task TryAutoStartZapretAsync()
        {
            if (!AppSettings.AutoStartZapret) return;

            try
            {
                // Check if strategy is configured
                var strategy = ServiceManager.GetInstalledStrategy();
                if (string.IsNullOrEmpty(strategy))
                {
                    System.Diagnostics.Debug.WriteLine("Auto-start skipped: no strategy configured");
                    return;
                }

                // Start service with current strategy
                await AppState.WinwsService.StartAsync($"--discord-youtube={strategy}");

                System.Diagnostics.Debug.WriteLine("Auto-start: Service started successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-start failed: {ex.Message}");
                // Show error toast if enabled
                if (Services.ToastNotifier.IsEnabled)
                {
                    Services.ToastNotifier.Show(
                        "Ошибка автозапуска",
                        $"Не удалось запустить сервис: {ex.Message}",
                        Services.ToastType.Error);
                }
            }
        }

        private void ExitApp()
        {
            AppState.WinwsService.Stop();
            _trayIcon?.Dispose();
            Application.Current.Exit();
        }
    }
}
