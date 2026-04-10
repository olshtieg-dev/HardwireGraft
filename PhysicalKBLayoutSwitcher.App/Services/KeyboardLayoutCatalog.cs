using Microsoft.Win32;
using PhysicalKBLayoutSwitcher.App.Models;
using PhysicalKBLayoutSwitcher.App.Win32;

namespace PhysicalKBLayoutSwitcher.App.Services;

public sealed class KeyboardLayoutCatalog
{
    private const string KeyboardLayoutsRegistryPath = @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts";
    private const string KeyboardLayoutSubstitutesRegistryPath = @"Keyboard Layout\Substitutes";

    public IReadOnlyList<KeyboardLayoutOption> GetAvailableLayouts()
    {
        return GetCandidateLayoutIds()
            .Where(layoutId => !string.IsNullOrWhiteSpace(layoutId))
            .Select(CreateLayoutOption)
            .DistinctBy(layout => layout.LayoutId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(layout => layout.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(layout => layout.LayoutId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string NormalizeLayoutId(string layoutId)
    {
        var normalizedLayoutId = layoutId.Trim();
        if (normalizedLayoutId.Length == 4)
        {
            return "0000" + normalizedLayoutId;
        }

        return normalizedLayoutId.ToUpperInvariant();
    }

    public static string ResolveLayoutId(string layoutId)
    {
        var normalizedLayoutId = NormalizeLayoutId(layoutId);
        if (LayoutExists(normalizedLayoutId))
        {
            return normalizedLayoutId;
        }

        using var substitutesKey = Registry.CurrentUser.OpenSubKey(KeyboardLayoutSubstitutesRegistryPath);
        var substitutedLayoutId = substitutesKey?.GetValue(normalizedLayoutId)?.ToString();
        if (!string.IsNullOrWhiteSpace(substitutedLayoutId))
        {
            var normalizedSubstitute = NormalizeLayoutId(substitutedLayoutId);
            if (LayoutExists(normalizedSubstitute))
            {
                return normalizedSubstitute;
            }
        }

        if (normalizedLayoutId.Length == 8)
        {
            var lowWordFallback = "0000" + normalizedLayoutId[^4..];
            if (LayoutExists(lowWordFallback))
            {
                return lowWordFallback;
            }
        }

        return normalizedLayoutId;
    }

    private static KeyboardLayoutOption CreateLayoutOption(string layoutId)
    {
        var resolvedLayoutId = ResolveLayoutId(layoutId);
        var displayName = GetLayoutDisplayName(resolvedLayoutId);

        return new KeyboardLayoutOption
        {
            LayoutId = resolvedLayoutId,
            DisplayName = $"{displayName} [{resolvedLayoutId}]",
        };
    }

    private static IEnumerable<string> GetCandidateLayoutIds()
    {
        using var keyboardLayoutsKey = Registry.LocalMachine.OpenSubKey(KeyboardLayoutsRegistryPath);
        if (keyboardLayoutsKey is not null)
        {
            foreach (var layoutId in keyboardLayoutsKey.GetSubKeyNames())
            {
                yield return layoutId;
            }
        }

        foreach (var loadedLayoutId in RawInputApi.GetKeyboardLayoutHandles().Select(RawInputApi.GetKeyboardLayoutId))
        {
            yield return loadedLayoutId;
        }

        using var preloadKey = Registry.CurrentUser.OpenSubKey(@"Keyboard Layout\Preload");
        if (preloadKey is null)
        {
            yield break;
        }

        foreach (var valueName in preloadKey.GetValueNames())
        {
            var configuredLayoutId = preloadKey.GetValue(valueName)?.ToString();
            if (!string.IsNullOrWhiteSpace(configuredLayoutId))
            {
                yield return configuredLayoutId;
            }
        }
    }

    private static bool LayoutExists(string layoutId)
    {
        using var keyboardLayoutsKey = Registry.LocalMachine.OpenSubKey(KeyboardLayoutsRegistryPath);
        using var layoutKey = keyboardLayoutsKey?.OpenSubKey(layoutId);
        return layoutKey is not null;
    }

    private static string GetLayoutDisplayName(string layoutId)
    {
        var normalizedLayoutId = ResolveLayoutId(layoutId);

        using var keyboardLayoutsKey = Registry.LocalMachine.OpenSubKey(KeyboardLayoutsRegistryPath);
        using var layoutKey = keyboardLayoutsKey?.OpenSubKey(normalizedLayoutId);
        var value = layoutKey?.GetValue("Layout Text")?.ToString();

        return string.IsNullOrWhiteSpace(value)
            ? normalizedLayoutId
            : value;
    }
}
