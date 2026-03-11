using System.Collections.Generic;
using ZUI.Services;

namespace ZUI
{
    public static class AppState
    {
        public static WinwsService WinwsService { get; } = new();
        public static string CurrentStrategy { get; set; } = "General";
        public static List<string> Logs { get; } = new();

        static AppState()
        {
            WinwsService.LogReceived += line =>
            {
                Logs.Add(line);
            };
        }
    }
}