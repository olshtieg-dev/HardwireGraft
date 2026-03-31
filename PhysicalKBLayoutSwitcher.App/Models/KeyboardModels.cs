namespace PhysicalKBLayoutSwitcher.App.Models;

public sealed class KeyboardDeviceSnapshot
{
    public required string DeviceHandle { get; init; }

    public required string DeviceName { get; set; }

    public bool IsConnected { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public int EventCount { get; set; }

    public int LastVirtualKey { get; set; }

    public string? LastVirtualKeyName { get; set; }

    public int LastMessage { get; set; }

    public string? LastMessageName { get; set; }
}

public sealed class KeyboardInputEvent : EventArgs
{
    public required string DeviceHandle { get; init; }

    public required string DeviceName { get; init; }

    public required int VirtualKey { get; init; }

    public required int Message { get; init; }

    public required int MakeCode { get; init; }

    public required int Flags { get; init; }

    public required bool IsKeyDown { get; init; }

    public required string VirtualKeyName { get; init; }

    public required string MessageName { get; init; }

    public required DateTimeOffset SeenAt { get; init; }
}
