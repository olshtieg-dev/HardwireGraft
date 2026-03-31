using Microsoft.Win32;

namespace PhysicalKBLayoutSwitcher.App.Services;

public sealed class AutoStartService
{
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PhysicalKBLayoutSwitcher";

    public bool IsEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: false);
        var configuredValue = runKey?.GetValue(ValueName)?.ToString();
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return false;
        }

        return string.Equals(configuredValue, BuildCommand(), StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunRegistryPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the Windows Run registry key.");

        if (enabled)
        {
            runKey.SetValue(ValueName, BuildCommand());
            return;
        }

        if (runKey.GetValue(ValueName) is not null)
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string BuildCommand()
    {
        var executablePath = Application.ExecutablePath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("Could not determine the application executable path.");
        }

        return $"\"{executablePath}\"";
    }
}
