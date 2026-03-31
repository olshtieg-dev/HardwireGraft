using System.Text.Json.Serialization;

namespace PhysicalKBLayoutSwitcher.App.Models;

public sealed class AppConfiguration
{
    [JsonPropertyName("autoStartPreferenceInitialized")]
    public bool AutoStartPreferenceInitialized { get; set; }

    [JsonPropertyName("autoStartEnabled")]
    public bool AutoStartEnabled { get; set; }

    [JsonPropertyName("deviceMappings")]
    public List<DeviceLayoutMapping> DeviceMappings { get; set; } = [];

    public AppConfiguration Clone()
    {
        return new AppConfiguration
        {
            AutoStartPreferenceInitialized = AutoStartPreferenceInitialized,
            AutoStartEnabled = AutoStartEnabled,
            DeviceMappings = DeviceMappings
                .Select(mapping => new DeviceLayoutMapping
                {
                    DeviceId = mapping.DeviceId,
                    DeviceName = mapping.DeviceName,
                    LayoutId = mapping.LayoutId,
                })
                .ToList(),
        };
    }

    public string? GetLayoutForDevice(string deviceId)
    {
        var mapping = DeviceMappings.FirstOrDefault(entry =>
            string.Equals(entry.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));

        return mapping?.LayoutId;
    }

    public void RemoveLayoutForDevice(string deviceId)
    {
        DeviceMappings.RemoveAll(entry =>
            string.Equals(entry.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
    }

    public void SetLayoutForDevice(string deviceId, string layoutId, string? deviceName = null)
    {
        var mapping = DeviceMappings.FirstOrDefault(entry =>
            string.Equals(entry.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));

        if (mapping is null)
        {
            DeviceMappings.Add(new DeviceLayoutMapping
            {
                DeviceId = deviceId,
                DeviceName = deviceName,
                LayoutId = layoutId,
            });

            return;
        }

        mapping.DeviceName = deviceName ?? mapping.DeviceName;
        mapping.LayoutId = layoutId;
    }
}

public sealed class DeviceLayoutMapping
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("layoutId")]
    public string LayoutId { get; set; } = string.Empty;
}
