namespace PhysicalKBLayoutSwitcher.App.Models;

public sealed class KeyboardLayoutOption
{
    public required string LayoutId { get; init; }

    public required string DisplayName { get; init; }

    public override string ToString()
    {
        return $"{DisplayName} ({LayoutId})";
    }
}
