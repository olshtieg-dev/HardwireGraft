namespace PhysicalKBLayoutSwitcher.App.Services;

public sealed class ShortcutService
{
    private const string ShortcutName = "Physical KB Layout Switcher.lnk";

    public void EnsureLaunchShortcuts()
    {
        var executablePath = Application.ExecutablePath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new InvalidOperationException("Could not determine the application executable path.");
        }

        EnsureShortcut(GetStartMenuShortcutPath(), executablePath);
        EnsureShortcut(GetDesktopShortcutPath(), executablePath);
    }

    private static string GetStartMenuShortcutPath()
    {
        var programsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var appDirectory = Path.Combine(programsDirectory, "Physical KB Layout Switcher");
        Directory.CreateDirectory(appDirectory);
        return Path.Combine(appDirectory, ShortcutName);
    }

    private static string GetDesktopShortcutPath()
    {
        var desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Path.Combine(desktopDirectory, ShortcutName);
    }

    private static void EnsureShortcut(string shortcutPath, string executablePath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Could not open Windows shortcut automation.");

        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Could not create Windows shortcut automation.");

        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = executablePath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty;
        shortcut.Description = "Start Physical KB Layout Switcher";
        shortcut.IconLocation = executablePath;
        shortcut.Save();
    }
}
