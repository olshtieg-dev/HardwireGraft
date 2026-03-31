using PhysicalKBLayoutSwitcher.App.Models;
using PhysicalKBLayoutSwitcher.App.Win32;

namespace PhysicalKBLayoutSwitcher.App.Services;

public sealed class RawKeyboardListener
{
    private readonly Dictionary<IntPtr, KeyboardDeviceSnapshot> knownDevices = [];

    public event EventHandler<KeyboardInputEvent>? KeyboardInputReceived;

    public event EventHandler? DevicesChanged;

    public IReadOnlyCollection<KeyboardDeviceSnapshot> KnownDevices => knownDevices.Values.ToArray();

    public void Attach(IntPtr windowHandle)
    {
        RawInputApi.RegisterKeyboardInputSink(windowHandle);
        RefreshDevices();
    }

    public bool ProcessWindowMessage(Message message)
    {
        switch (message.Msg)
        {
            case RawInputApi.WM_INPUT:
                ProcessRawKeyboardInput(message.LParam);
                return true;

            case RawInputApi.WM_INPUT_DEVICE_CHANGE:
                ProcessDeviceChange(message.WParam.ToInt32(), message.LParam);
                return true;

            default:
                return false;
        }
    }

    public void RefreshDevices()
    {
        var connectedHandles = RawInputApi.GetKeyboardDevices().ToHashSet();
        var changed = false;

        foreach (var deviceHandle in connectedHandles)
        {
            changed |= TrackDevice(deviceHandle, isConnected: true);
        }

        foreach (var entry in knownDevices)
        {
            if (connectedHandles.Contains(entry.Key) || !entry.Value.IsConnected)
            {
                continue;
            }

            entry.Value.IsConnected = false;
            changed = true;
        }

        if (changed)
        {
            OnDevicesChanged();
        }
    }

    private void ProcessDeviceChange(int changeType, IntPtr deviceHandle)
    {
        var changed = changeType switch
        {
            RawInputApi.GIDC_ARRIVAL => TrackDevice(deviceHandle, isConnected: true),
            RawInputApi.GIDC_REMOVAL => MarkDisconnected(deviceHandle),
            _ => false,
        };

        if (changed)
        {
            OnDevicesChanged();
        }
    }

    private void ProcessRawKeyboardInput(IntPtr rawInputHandle)
    {
        var rawInput = RawInputApi.ReadRawInput(rawInputHandle);
        if (rawInput.Header.Type != RawInputApi.RIM_TYPEKEYBOARD)
        {
            return;
        }

        var deviceName = RawInputApi.GetDeviceName(rawInput.Header.Device);
        if (DeviceIdentity.IsLikelyNoiseDevice(deviceName))
        {
            return;
        }

        var device = TrackDeviceSnapshot(rawInput.Header.Device, isConnected: true);
        var seenAt = DateTimeOffset.Now;
        var message = (int)rawInput.Keyboard.Message;
        var messageName = RawInputApi.GetKeyboardMessageName(rawInput.Keyboard.Message);
        var virtualKeyName = RawInputApi.GetVirtualKeyName(rawInput.Keyboard.VirtualKey);

        device.EventCount += 1;
        device.IsConnected = true;
        device.LastSeenAt = seenAt;
        device.LastVirtualKey = rawInput.Keyboard.VirtualKey;
        device.LastVirtualKeyName = virtualKeyName;
        device.LastMessage = message;
        device.LastMessageName = messageName;

        KeyboardInputReceived?.Invoke(this, new KeyboardInputEvent
        {
            DeviceHandle = device.DeviceHandle,
            DeviceName = device.DeviceName,
            VirtualKey = rawInput.Keyboard.VirtualKey,
            Message = message,
            MakeCode = rawInput.Keyboard.MakeCode,
            Flags = rawInput.Keyboard.Flags,
            IsKeyDown = RawInputApi.IsKeyDownMessage(rawInput.Keyboard.Message),
            VirtualKeyName = virtualKeyName,
            MessageName = messageName,
            SeenAt = seenAt,
        });
    }

    private bool TrackDevice(IntPtr deviceHandle, bool isConnected)
    {
        var existing = knownDevices.TryGetValue(deviceHandle, out var snapshot)
            ? snapshot
            : null;

        if (existing is null)
        {
            TrackDeviceSnapshot(deviceHandle, isConnected);
            return true;
        }

        var changed = false;
        var deviceName = RawInputApi.GetDeviceName(deviceHandle);
        if (DeviceIdentity.IsLikelyNoiseDevice(deviceName))
        {
            return false;
        }

        if (!string.Equals(existing.DeviceName, deviceName, StringComparison.Ordinal))
        {
            existing.DeviceName = deviceName;
            changed = true;
        }

        if (existing.IsConnected != isConnected)
        {
            existing.IsConnected = isConnected;
            changed = true;
        }

        return changed;
    }

    private KeyboardDeviceSnapshot TrackDeviceSnapshot(IntPtr deviceHandle, bool isConnected)
    {
        if (knownDevices.TryGetValue(deviceHandle, out var existing))
        {
            existing.IsConnected = isConnected;
            return existing;
        }

        var deviceName = RawInputApi.GetDeviceName(deviceHandle);
        if (DeviceIdentity.IsLikelyNoiseDevice(deviceName))
        {
            return new KeyboardDeviceSnapshot
            {
                DeviceHandle = deviceHandle.ToString("X"),
                DeviceName = deviceName,
                IsConnected = false,
                LastSeenAt = default,
                EventCount = 0,
                LastVirtualKey = 0,
                LastMessage = 0,
            };
        }

        var snapshot = new KeyboardDeviceSnapshot
        {
            DeviceHandle = deviceHandle.ToString("X"),
            DeviceName = deviceName,
            IsConnected = isConnected,
            LastSeenAt = default,
            EventCount = 0,
            LastVirtualKey = 0,
            LastMessage = 0,
        };

        knownDevices[deviceHandle] = snapshot;
        return snapshot;
    }

    private bool MarkDisconnected(IntPtr deviceHandle)
    {
        if (!knownDevices.TryGetValue(deviceHandle, out var device))
        {
            return false;
        }

        if (!device.IsConnected)
        {
            return false;
        }

        device.IsConnected = false;
        return true;
    }

    private void OnDevicesChanged()
    {
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }
}
