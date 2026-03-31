using System.Text.Json;
using PhysicalKBLayoutSwitcher.App.Models;

namespace PhysicalKBLayoutSwitcher.App.Services;

public sealed class ConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string configFilePath;

    public ConfigurationStore()
    {
        var configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhysicalKBLayoutSwitcher");

        Directory.CreateDirectory(configDirectory);
        configFilePath = Path.Combine(configDirectory, "config.json");
    }

    public string ConfigFilePath => configFilePath;

    public AppConfiguration Load()
    {
        if (!File.Exists(configFilePath))
        {
            return new AppConfiguration();
        }

        try
        {
            var json = File.ReadAllText(configFilePath);
            return JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions) ?? new AppConfiguration();
        }
        catch
        {
            return new AppConfiguration();
        }
    }

    public void Save(AppConfiguration configuration)
    {
        var json = JsonSerializer.Serialize(configuration, JsonOptions);
        File.WriteAllText(configFilePath, json);
    }
}
