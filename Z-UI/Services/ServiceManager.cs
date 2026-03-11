using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using ZUI.Services;

namespace ZUI.Services
{
    public static class ServiceManager
    {
        private const string ServiceName = "zapret";

        public static async Task<string> GetStatusAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var processes = Process.GetProcessesByName(ServiceName);
                    if (processes.Length > 0)
                    {
                        var strategy = GetInstalledStrategy();
                        if (!string.IsNullOrEmpty(strategy))
                            return $"Запущена ✓ (стратегия: {strategy})";
                        return "Запущена ✓";
                    }

                    return "Не установлена";
                }
                catch { return "Не установлена"; }
            });
        }

        public static bool IsInstalled()
        {
            try { var processes = Process.GetProcessesByName(ServiceName); return processes.Length > 0; }
            catch { return false; }
        }

        public static string GetInstalledStrategy()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"System\CurrentControlSet\Services\zapret");
                return key?.GetValue("zapret-discord-youtube")?.ToString() ?? "";
            }
            catch { return ""; }
        }

        public static async Task InstallAsync(string strategyName, string arguments, Action<string> log)
        {
            await Task.Run(() =>
            {
                try
                {
                    log("Останавливаю службу...");
                    RunSc($"stop {ServiceName}");
                    log("Удаляю старую службу...");
                    RunSc($"delete {ServiceName}");
                    log("Создаю службу...");
                    RunSc($"create {ServiceName} binPath= \"\\\"{ZapretPaths.WinwsExe}\\\" {arguments}\" DisplayName= \"zapret\" start= auto");
                    RunSc($"description {ServiceName} \"Zapret DPI bypass software\"");
                    log("Запускаю службу...");
                    RunSc($"start {ServiceName}");

                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                        @"System\CurrentControlSet\Services\zapret", true);
                    key?.SetValue("zapret-discord-youtube", strategyName,
                        Microsoft.Win32.RegistryValueKind.String);

                    log("✓ Служба успешно установлена и запущена");
                }
                catch (Exception ex) { log($"✗ Ошибка: {ex.Message}"); }
            });
        }

        public static async Task RemoveAsync(Action<string> log)
        {
            await Task.Run(() =>
            {
                try
                {
                    log("Останавливаю zapret...");
                    RunSc($"stop {ServiceName}");
                    log("Удаляю zapret...");
                    RunSc($"delete {ServiceName}");

                    foreach (var p in Process.GetProcessesByName("winws"))
                    { p.Kill(); log("winws.exe завершён"); }

                    log("Удаляю WinDivert...");
                    RunSc("stop WinDivert");
                    RunSc("delete WinDivert");
                    RunSc("stop WinDivert14");
                    RunSc("delete WinDivert14");
                    log("✓ Службы успешно удалены");
                }
                catch (Exception ex) { log($"✗ Ошибка: {ex.Message}"); }
            });
        }

        private static void RunSc(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("sc", args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi)!;
                p.WaitForExit(5000);
            }
            catch { }
        }
    }
}
