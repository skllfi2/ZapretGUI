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

        /// <summary>Главное окно приложения (для FilePicker и т.п.).</summary>
        public Window? MainWindow => _window;           // ← ДОБАВЛЕНО

        public App()
        {
            InitializeComponent();
        }

                protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var log = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Z-UI-log.txt");

            try
            {
                System.IO.File.WriteAllText(log, "Step 1: OnLaunched started\n");

                _window = new MainWindow();
                System.IO.File.AppendAllText(log, "Step 2: MainWindow created\n");

                _window.Activate();
                System.IO.File.AppendAllText(log, "Step 3: Activated\n");

                _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
                _window.Closed += OnWindowClosed;
                System.IO.File.AppendAllText(log, "Step 4: HWND obtained\n");

                Microsoft.UI.Xaml.ElementSoundPlayer.State = Microsoft.UI.Xaml.ElementSoundPlayerState.On;
                Microsoft.UI.Xaml.ElementSoundPlayer.SpatialAudioMode = Microsoft.UI.Xaml.ElementSpatialAudioMode.Off;
                System.IO.File.AppendAllText(log, "Step 5: SoundPlayer set\n");

                AppState.WinwsService.SetDispatcherQueue(_window.DispatcherQueue);
                System.IO.File.AppendAllText(log, "Step 6: DispatcherQueue set\n");

                Services.ToastNotifier.Initialize(_hwnd);
                System.IO.File.AppendAllText(log, "Step 7: ToastNotifier initialized\n");

                _trayIcon = new TrayIcon(_hwnd,
                    System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Z-UI.ico"),
                    "Z-UI — Остановлено",
                    onShow: ShowMainWindow,
                    onExit: ExitApp);
                TrayIcon = _trayIcon;
                System.IO.File.AppendAllText(log, "Step 8: TrayIcon created\n");

                AppState.WinwsService.StatusChanged += isRunning =>
                {
                    _trayIcon?.UpdateStatus(isRunning);
                    if (Services.ToastNotifier.IsEnabled)
                        Services.ToastNotifier.Show(
                            "Статус сервиса",
                            isRunning ? "Запущен" : "Остановлен",
                            Services.ToastType.Success);
                };
                System.IO.File.AppendAllText(log, "Step 9: StatusChanged subscribed\n");

                if (AppSettings.AutoUpdateCheck)
                    _ = Task.Run(async () => await Services.UpdateChecker.CheckAsync());

                if (AppSettings.AutoStartZapret)
                    _ = TryAutoStartZapretAsync();

                System.IO.File.AppendAllText(log, "Step 10: DONE\n");
            }
            catch (Exception ex)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== CRASH ===");
                var current = ex;
                int depth = 0;
                while (current != null)
                {
                    sb.AppendLine($"[{depth}] {current.GetType().FullName}: {current.Message}");
                    if (current.Data.Contains("RestrictedDescription"))
                        sb.AppendLine($"    RestrictedDescription: {current.Data["RestrictedDescription"]}");
                    sb.AppendLine($"    HResult: 0x{current.HResult:X8}");
                    sb.AppendLine($"    Stack: {current.StackTrace}");
                    current = current.InnerException;
                    depth++;
                }
                System.IO.File.AppendAllText(log, sb.ToString());
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
