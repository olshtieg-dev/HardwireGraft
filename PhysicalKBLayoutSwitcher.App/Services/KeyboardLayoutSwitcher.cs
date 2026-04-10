using PhysicalKBLayoutSwitcher.App.Win32;

namespace PhysicalKBLayoutSwitcher.App.Services;

public sealed class KeyboardLayoutSwitcher
{
    public bool TrySwitchToLayout(string layoutId)
    {
        var normalizedLayoutId = KeyboardLayoutCatalog.ResolveLayoutId(layoutId);
        if (string.IsNullOrWhiteSpace(normalizedLayoutId))
        {
            return false;
        }

        var currentLayoutId = KeyboardLayoutCatalog.ResolveLayoutId(RawInputApi.GetForegroundKeyboardLayoutId());
        if (string.Equals(currentLayoutId, normalizedLayoutId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var keyboardLayoutHandle = RawInputApi.LoadKeyboardLayout(
            normalizedLayoutId,
            RawInputApi.KLF_ACTIVATE | RawInputApi.KLF_REPLACELANG);

        if (keyboardLayoutHandle == IntPtr.Zero)
        {
            return false;
        }

        RawInputApi.ActivateKeyboardLayout(keyboardLayoutHandle, 0);

        var foregroundWindow = RawInputApi.GetForegroundWindow();
        if (foregroundWindow != IntPtr.Zero)
        {
            RawInputApi.PostMessage(
                foregroundWindow,
                RawInputApi.WM_INPUTLANGCHANGEREQUEST,
                new IntPtr(RawInputApi.INPUTLANGCHANGE_SYSCHARSET),
                keyboardLayoutHandle);
        }

        return true;
    }
}
