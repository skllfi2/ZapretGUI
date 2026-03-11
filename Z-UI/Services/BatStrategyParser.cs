using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ZUI.Services
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

            // ── Game Filter ───────────────────────────────────────────
            // service.bat логика:
            //   all  → GameFilter=1024-65535, GameFilterTCP=1024-65535, GameFilterUDP=1024-65535
            //   tcp  → GameFilter=1024-65535, GameFilterTCP=1024-65535, GameFilterUDP=12 (пустышка)
            //   udp  → GameFilter=1024-65535, GameFilterTCP=12 (пустышка), GameFilterUDP=1024-65535
            //   disabled → все удаляем

            var gameFilter = AppSettings.GameFilter ?? "disabled";

            string gameTcp, gameUdp, gameAll;
            switch (gameFilter)
            {
                case "all":
                    gameAll = "1024-65535"; gameTcp = "1024-65535"; gameUdp = "1024-65535";
                    break;
                case "tcp":
                    gameAll = "1024-65535"; gameTcp = "1024-65535"; gameUdp = ""; // пустышка → удаляем
                    break;
                case "udp":
                    gameAll = "1024-65535"; gameTcp = ""; gameUdp = "1024-65535"; // пустышка → удаляем
                    break;
                default: // disabled
                    gameAll = ""; gameTcp = ""; gameUdp = "";
                    break;
            }

            args = SubstituteOrRemove(args, "%GameFilter%",    gameAll,  new[] { "--wf-tcp", "--wf-udp", "--filter-tcp", "--filter-udp" });
            args = SubstituteOrRemove(args, "%GameFilterTCP%", gameTcp,  new[] { "--wf-tcp", "--filter-tcp" });
            args = SubstituteOrRemove(args, "%GameFilterUDP%", gameUdp,  new[] { "--wf-udp", "--filter-udp" });

            // Убираем висячий --new в конце
            args = Regex.Replace(args, @"--new\s*$", "");

            // Нормализуем пробелы
            args = args.Trim();
            args = Regex.Replace(args, @"\s+", " ");

            return args;
        }

        private static string SubstituteOrRemove(string args, string placeholder, string value, string[] flagPrefixes)
        {
            if (!string.IsNullOrEmpty(value))
                return args.Replace(placeholder, value);

            // Удаляем --flag=PLACEHOLDER (с любым из указанных флагов)
            foreach (var prefix in flagPrefixes)
                args = Regex.Replace(args, $@"{Regex.Escape(prefix)}={Regex.Escape(placeholder)}\s*", "");

            // Удаляем из перечисления: ,PLACEHOLDER или PLACEHOLDER,
            args = Regex.Replace(args, $@",\s*{Regex.Escape(placeholder)}", "");
            args = Regex.Replace(args, $@"{Regex.Escape(placeholder)}\s*,\s*", "");

            // Удаляем остаток если не нашли
            args = args.Replace(placeholder, "");

            return args;
        }

        // ── IPSet Filter ──────────────────────────────────────────────
        // Повторяет логику service.bat: меняем содержимое ipset-all.txt
        //   loaded → реальный список IP (восстанавливаем из .backup)
        //   none   → пустышка 203.0.113.113/32 (winws ничего не фильтрует по IP)
        //   any    → пустой файл (winws игнорирует ipset совсем)

        public static void ApplyIpsetFilter(string mode)
        {
            var listFile   = Path.Combine(ZapretPaths.ListsDir, "ipset-all.txt");
            var backupFile = listFile + ".backup";

            Directory.CreateDirectory(ZapretPaths.ListsDir);

            switch (mode)
            {
                case "loaded":
                    if (File.Exists(backupFile))
                    {
                        if (File.Exists(listFile)) File.Delete(listFile);
                        File.Copy(backupFile, listFile);
                    }
                    break;

                case "none":
                    BackupIfReal(listFile, backupFile);
                    File.WriteAllText(listFile, "203.0.113.113/32\n");
                    break;

                case "any":
                    BackupIfReal(listFile, backupFile);
                    File.WriteAllText(listFile, "");
                    break;
            }
        }

        private static void BackupIfReal(string listFile, string backupFile)
        {
            if (!File.Exists(listFile)) return;
            var content = File.ReadAllText(listFile).Trim();
            if (content != "203.0.113.113/32" && content != "")
                File.Copy(listFile, backupFile, overwrite: true);
        }

        // Определяет текущий режим по содержимому файла (как в service.bat)
        public static string GetCurrentIpsetMode()
        {
            var listFile = Path.Combine(ZapretPaths.ListsDir, "ipset-all.txt");
            if (!File.Exists(listFile)) return "any";
            var content = File.ReadAllText(listFile).Trim();
            if (content == "") return "any";
            if (content == "203.0.113.113/32") return "none";
            return "loaded";
        }
    }
}
