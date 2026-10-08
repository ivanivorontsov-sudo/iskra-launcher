using System;
using System.IO;

namespace Iskra.App.Services;

/// <summary>
/// Маркер «приложение работает». Если он уже есть при старте — прошлый сеанс завершился аварийно,
/// значит включаем безопасный режим. Удаляется при чистом выходе.
/// </summary>
internal static class RunState
{
    private static string Marker => Path.Combine(Paths.Root, "running.lock");

    public static bool CrashedLastTime { get; private set; }

    public static void Begin()
    {
        try
        {
            CrashedLastTime = File.Exists(Marker);
            File.WriteAllText(Marker, DateTime.UtcNow.ToString("o"));
        }
        catch { }
    }

    public static void End()
    {
        try { if (File.Exists(Marker)) File.Delete(Marker); } catch { }
    }
}
