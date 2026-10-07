using System;
using System.IO;

namespace Iskra.App.Services;

/// <summary>Экран блокировки через UWP-API Windows.System.UserProfile.LockScreen (x64, без прав администратора).</summary>
internal static class LockScreenManager
{
    public static bool TrySet(string imagePath)
    {
        try
        {
            if (!Environment.Is64BitProcess) { Log.Warn("Экран блокировки: нужен 64-битный процесс"); return false; }
            var file = Windows.Storage.StorageFile.GetFileFromPathAsync(imagePath).AsTask().GetAwaiter().GetResult();
            Windows.System.UserProfile.LockScreen.SetImageFileAsync(file).AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex) { Log.Error("Экран блокировки SetImageFileAsync", ex); return false; }
    }

    public static string? Backup()
    {
        try
        {
            var current = Windows.System.UserProfile.LockScreen.OriginalImageFile;
            if (current == null) return null;
            var src = current.IsFile ? current.LocalPath : current.AbsolutePath;
            if (string.IsNullOrEmpty(src) || !File.Exists(src)) return null;
            var dst = Paths.LockScreenBackup + Path.GetExtension(src);
            File.Copy(src, dst, true);
            return dst;
        }
        catch (Exception ex) { Log.Error("Резерв экрана блокировки", ex); return null; }
    }
}
