using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ZUI.Services;

namespace ZUI.Services
{
    public class WinwsService
    {
        private Process? _process;
        private DispatcherQueue? _dispatcherQueue;

        public bool IsRunning => _process != null && !_process.HasExited;
        public event Action<string>? LogReceived;
        public event Action<bool>? StatusChanged;

        public void SetDispatcherQueue(DispatcherQueue queue)
        {
            _dispatcherQueue = queue;
        }

        private void PlaySound(Microsoft.UI.Xaml.ElementSoundKind kind)
        {
            if (!AppSettings.SoundEffects) return;
            _dispatcherQueue?.TryEnqueue(() =>
                Microsoft.UI.Xaml.ElementSoundPlayer.Play(kind));
        }

        public async Task StartAsync(string arguments)
        {
            if (IsRunning) return;

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ZapretPaths.WinwsExe,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = ZapretPaths.WinwsDir
                }
            };

            _process.OutputDataReceived += (s, e) => { if (e.Data != null) LogReceived?.Invoke(e.Data); };
            _process.ErrorDataReceived += (s, e) => { if (e.Data != null) LogReceived?.Invoke(e.Data); };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            StatusChanged?.Invoke(true);
            PlaySound(Microsoft.UI.Xaml.ElementSoundKind.Invoke);

            // Show toast notification for service start
            if (ToastNotifier.IsEnabled)
            {
                ToastNotifier.Show(
                    "Сервис запущен",
                    "Z-UI успешно запущен",
                    ToastType.Success);
            }

            await Task.CompletedTask;
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _process?.Kill();
            _process = null;
            StatusChanged?.Invoke(false);
            PlaySound(Microsoft.UI.Xaml.ElementSoundKind.Hide);

            // Show toast notification for service stop
            if (ToastNotifier.IsEnabled)
            {
                ToastNotifier.Show(
                    "Сервис остановлен",
                    "Z-UI остановлен",
                    ToastType.Informational);
            }
        }
    }
}
