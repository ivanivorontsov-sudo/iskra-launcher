using System;
using System.IO;

namespace Iskra.App.Services;

internal static class Log
{
    private static readonly object Lock = new();
    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);
    public static void Error(string msg, Exception? ex = null) => Write("ERR ", ex == null ? msg : $"{msg} :: {ex}");

    private static void Write(string level, string msg)
    {
        try
        {
            lock (Lock)
            {
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}{Environment.NewLine}";
                File.AppendAllText(Paths.Log, line);
                var fi = new FileInfo(Paths.Log);
                if (fi.Exists && fi.Length > 1_000_000)
                {
                    var txt = File.ReadAllText(Paths.Log);
                    File.WriteAllText(Paths.Log, txt[(txt.Length / 2)..]);
                }
            }
        }
        catch { /* логирование не должно падать */ }
    }
}
