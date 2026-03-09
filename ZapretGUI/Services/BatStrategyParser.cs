using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ZapretGUI.Services
{
    public static class BatStrategyParser
    {
        public static string? ParseStrategy(string batFilePath)
        {
            return ExtractArguments(batFilePath, ZapretPaths.WinwsDir + "\\", ZapretPaths.ListsDir + "\\");
        }

        public static string? ExtractArguments(string batFilePath, string binPath, string listsPath)
        {
            if (!File.Exists(batFilePath)) return null;

            var content = File.ReadAllText(batFilePath);

            var match = Regex.Match(content,
                @"start\s+""[^""]*""\s+/min\s+""[^""]*winws\.exe""\s+([\s\S]+?)(?=\r?\n\r?\n|\z)",
                RegexOptions.IgnoreCase);

            if (!match.Success) return null;

            var args = match.Groups[1].Value;

            // Убираем продолжение строк ^
            args = Regex.Replace(args, @"\s*\^\s*\r?\n\s*", " ");

            // Подставляем пути
            args = args.Replace("%BIN%", binPath)
                       .Replace("%LISTS%", listsPath);

            // Применяем Game Filter из настроек
            var gameFilter = AppSettings.GameFilter ?? "disabled";
            var (gameTcp, gameUdp) = gameFilter switch
            {
                "all" => ("1024-65535", "1024-65535"),
                "tcp" => ("1024-65535", ""),
                "udp" => ("", "1024-65535"),
                _ => ("", "") // disabled
            };

            if (!string.IsNullOrEmpty(gameTcp))
            {
                args = args.Replace("%GameFilterTCP%", gameTcp);
            }
            else
            {
                args = Regex.Replace(args, @",\s*%GameFilterTCP%", "");
                args = Regex.Replace(args, @"%GameFilterTCP%\s*,\s*", "");
                args = Regex.Replace(args, @"--filter-tcp=%GameFilterTCP%\s*", "");
                args = args.Replace("%GameFilterTCP%", "");
            }

            if (!string.IsNullOrEmpty(gameUdp))
            {
                args = args.Replace("%GameFilterUDP%", gameUdp);
            }
            else
            {
                args = Regex.Replace(args, @",\s*%GameFilterUDP%", "");
                args = Regex.Replace(args, @"%GameFilterUDP%\s*,\s*", "");
                args = Regex.Replace(args, @"--filter-udp=%GameFilterUDP%\s*", "");
                args = args.Replace("%GameFilterUDP%", "");
            }

            // Применяем IPSet Filter из настроек
            var ipsetFilter = AppSettings.IpsetFilter ?? "any";
            if (ipsetFilter != "any" && !args.Contains("--ipset-sflag"))
            {
                var newIndex = args.IndexOf("--new", StringComparison.OrdinalIgnoreCase);
                if (newIndex > 0)
                    args = args.Insert(newIndex, $"--ipset-sflag={ipsetFilter} ");
                else
                    args += $" --ipset-sflag={ipsetFilter}";
            }

            // Убираем висячий --new в конце
            args = Regex.Replace(args, @"--new\s*$", "");

            // Нормализуем пробелы
            args = args.Trim();
            args = Regex.Replace(args, @"\s+", " ");

            return args;
        }
    }
}
