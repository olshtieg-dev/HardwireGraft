using System.Runtime.InteropServices;
using System.Text;

namespace PhysicalKBLayoutSwitcher.App.Win32;

internal static class RawInputApi
{
    public const int WM_INPUT = 0x00FF;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;
    public const int WM_INPUT_DEVICE_CHANGE = 0x00FE;
    public const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
    public const int RID_INPUT = 0x10000003;
    public const int RIM_TYPEKEYBOARD = 1;
    public const int RIDI_DEVICENAME = 0x20000007;
    public const uint RIDEV_INPUTSINK = 0x00000100;
    public const uint RIDEV_DEVNOTIFY = 0x00002000;
    public const int GIDC_ARRIVAL = 1;
    public const int GIDC_REMOVAL = 2;
    public const uint KLF_ACTIVATE = 0x00000001;
    public const uint KLF_SUBSTITUTE_OK = 0x00000002;
    public const uint INPUTLANGCHANGE_SYSCHARSET = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    public struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;

        public RawInputDevice(ushort usagePage, ushort usage, uint flags, IntPtr target)
        {
            UsagePage = usagePage;
            Usage = usage;
            Flags = flags;
            Target = target;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RawInputDeviceListItem
    {
        public IntPtr Device;
        public int Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RawInputHeader
    {
        public int Type;
        public int Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VirtualKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RawInput
    {
        public RawInputHeader Header;
        public RawKeyboard Keyboard;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] RawInputDevice[] devices,
        uint deviceCount,
        uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInputHandle,
        uint command,
        IntPtr data,
        ref uint size,
        uint headerSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        [Out] RawInputDeviceListItem[]? deviceList,
        ref uint deviceCount,
        uint size);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr device,
        uint command,
        StringBuilder? data,
        ref uint size);

    [DllImport("user32.dll")]
    private static extern uint GetKeyboardLayoutList(int bufferCount, [Out] IntPtr[]? buffer);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "ActivateKeyboardLayout")]
    private static extern IntPtr ActivateKeyboardLayoutNative(IntPtr keyboardLayoutHandle, uint flags);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "LoadKeyboardLayoutW", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadKeyboardLayoutNative(string keyboardLayoutId, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    private static extern IntPtr GetForegroundWindowNative();

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "PostMessageW")]
    private static extern bool PostMessageNative(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    public static void RegisterKeyboardInputSink(IntPtr targetHandle)
    {
        var devices = new[]
        {
            new RawInputDevice(1, 6, RIDEV_INPUTSINK | RIDEV_DEVNOTIFY, targetHandle),
        };

        var ok = RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>());
        if (!ok)
        {
            throw new InvalidOperationException("RegisterRawInputDevices failed.");
        }
    }

    public static RawInput ReadRawInput(IntPtr rawInputHandle)
    {
        uint size = 0;
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();

        var probe = GetRawInputData(rawInputHandle, RID_INPUT, IntPtr.Zero, ref size, headerSize);
        if (probe == uint.MaxValue || size == 0)
        {
            throw new InvalidOperationException("GetRawInputData failed while probing buffer size.");
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var result = GetRawInputData(rawInputHandle, RID_INPUT, buffer, ref size, headerSize);
            if (result == uint.MaxValue)
            {
                throw new InvalidOperationException("GetRawInputData failed while reading keyboard input.");
            }

            return Marshal.PtrToStructure<RawInput>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static IReadOnlyList<IntPtr> GetKeyboardDevices()
    {
        uint count = 0;
        var elementSize = (uint)Marshal.SizeOf<RawInputDeviceListItem>();
        var probe = GetRawInputDeviceList(null, ref count, elementSize);
        if (probe != 0)
        {
            throw new InvalidOperationException("GetRawInputDeviceList failed while probing device count.");
        }

        if (count == 0)
        {
            return [];
        }

        var devices = new RawInputDeviceListItem[count];
        var result = GetRawInputDeviceList(devices, ref count, elementSize);
        if (result == uint.MaxValue)
        {
            throw new InvalidOperationException("GetRawInputDeviceList failed while reading devices.");
        }

        return devices
            .Where(item => item.Type == RIM_TYPEKEYBOARD)
            .Select(item => item.Device)
            .ToArray();
    }

    public static string GetDeviceName(IntPtr deviceHandle)
    {
        if (deviceHandle == IntPtr.Zero)
        {
            return "Unknown keyboard device";
        }

        uint size = 0;
        _ = GetRawInputDeviceInfo(deviceHandle, RIDI_DEVICENAME, null, ref size);
        if (size == 0)
        {
            return deviceHandle.ToString("X");
        }

        var builder = new StringBuilder((int)size);
        var result = GetRawInputDeviceInfo(deviceHandle, RIDI_DEVICENAME, builder, ref size);
        if (result == uint.MaxValue)
        {
            return deviceHandle.ToString("X");
        }

        return builder.ToString();
    }

    public static IReadOnlyList<IntPtr> GetKeyboardLayoutHandles()
    {
        var count = (int)GetKeyboardLayoutList(0, null);
        if (count <= 0)
        {
            return [];
        }

        var handles = new IntPtr[count];
        var actualCount = (int)GetKeyboardLayoutList(handles.Length, handles);
        if (actualCount <= 0)
        {
            return [];
        }

        if (actualCount == handles.Length)
        {
            return handles;
        }

        return handles.Take(actualCount).ToArray();
    }

    public static string GetKeyboardLayoutId(IntPtr keyboardLayoutHandle)
    {
        if (keyboardLayoutHandle == IntPtr.Zero)
        {
            return string.Empty;
        }

        var value = unchecked((uint)keyboardLayoutHandle.ToInt64());
        return value.ToString("X8");
    }

    public static string GetForegroundKeyboardLayoutId()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return GetKeyboardLayoutId(GetKeyboardLayout(0));
        }

        var threadId = GetWindowThreadProcessId(foregroundWindow, out _);
        return GetKeyboardLayoutId(GetKeyboardLayout(threadId));
    }

    public static bool IsKeyDownMessage(uint message)
    {
        return message is WM_KEYDOWN or WM_SYSKEYDOWN;
    }

    public static string GetKeyboardMessageName(uint message)
    {
        return message switch
        {
            WM_KEYDOWN => nameof(WM_KEYDOWN),
            WM_KEYUP => nameof(WM_KEYUP),
            WM_SYSKEYDOWN => nameof(WM_SYSKEYDOWN),
            WM_SYSKEYUP => nameof(WM_SYSKEYUP),
            _ => $"0x{message:X4}",
        };
    }

    public static string GetVirtualKeyName(ushort virtualKey)
    {
        var key = (Keys)virtualKey;
        var keyName = key.ToString();
        return string.IsNullOrWhiteSpace(keyName)
            ? $"0x{virtualKey:X2}"
            : keyName;
    }

    public static IntPtr LoadKeyboardLayout(string keyboardLayoutId, uint flags)
    {
        return LoadKeyboardLayoutNative(keyboardLayoutId, flags);
    }

    public static IntPtr ActivateKeyboardLayout(IntPtr keyboardLayoutHandle, uint flags)
    {
        return ActivateKeyboardLayoutNative(keyboardLayoutHandle, flags);
    }

    public static IntPtr GetForegroundWindow()
    {
        return GetForegroundWindowNative();
    }

    public static bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam)
    {
        return PostMessageNative(windowHandle, message, wParam, lParam);
    }
}
