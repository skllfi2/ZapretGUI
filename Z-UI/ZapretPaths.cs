using System;
using System.IO;

namespace ZUI
{
    public static class ZapretPaths
    {
        public static string AppDir => AppContext.BaseDirectory;
        public static string ZapretDir => Path.Combine(AppDir, "zapret");
        public static string WinwsDir => Path.Combine(ZapretDir, "winws");
        public static string WinwsExe => Path.Combine(WinwsDir, "winws.exe");
        public static string ListsDir => Path.Combine(ZapretDir, "lists");
        public static string StrategiesDir => Path.Combine(ZapretDir, "strategies");
        public static string UtilsDir => Path.Combine(ZapretDir, "utils");
        public static string VersionFile => Path.Combine(ZapretDir, "version.txt");

        public static string LocalVersion
        {
            get
            {
                try
                {
                    if (File.Exists(VersionFile))
                        return File.ReadAllText(VersionFile).Trim();
                }
                catch { }
                return "неизвестно";
            }
        }
    }
}
