namespace PhysicalKBLayoutSwitcher.App.Services;

public static class DeviceIdentity
{
    public static bool IsLikelyNoiseDevice(string deviceName)
    {
        return deviceName.Contains("WmVirtualDevice", StringComparison.OrdinalIgnoreCase)
            || deviceName.Contains("ROOT#RDP_KBD", StringComparison.OrdinalIgnoreCase)
            || deviceName.Contains("Remote Desktop", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetDisplayName(string deviceName)
    {
        if (deviceName.Contains("WmVirtualDevice", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows Virtual Keyboard Device";
        }

        if (deviceName.Contains("ROOT#RDP_KBD", StringComparison.OrdinalIgnoreCase))
        {
            return "Remote Desktop Keyboard";
        }

        return deviceName;
    }
}
