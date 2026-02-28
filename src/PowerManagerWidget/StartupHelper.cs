using System.IO;

namespace PowerManagerWidget;

/// <summary>
/// Добавление и удаление ярлыка в папке «Автозагрузка» Windows (текущий пользователь).
/// Рабочая папка ярлыка = папка exe, чтобы виджет находил scheme-guids.json.
/// </summary>
public static class StartupHelper
{
    private const string ShortcutName = "Power Manager.lnk";

    public static string GetStartupShortcutPath()
    {
        var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        return Path.Combine(startup, ShortcutName);
    }

    public static bool IsRunAtStartup()
    {
        return File.Exists(GetStartupShortcutPath());
    }

    public static void SetRunAtStartup(bool enable)
    {
        var shortcutPath = GetStartupShortcutPath();
        if (enable)
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return;
            var workDir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(workDir))
                return;

            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                    return;
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = workDir;
                shortcut.Save();
            }
            catch
            {
                // Нет прав или WScript.Shell недоступен
            }
        }
        else
        {
            try
            {
                if (File.Exists(shortcutPath))
                    File.Delete(shortcutPath);
            }
            catch { }
        }
    }
}
