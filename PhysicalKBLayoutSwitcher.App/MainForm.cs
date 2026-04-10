using PhysicalKBLayoutSwitcher.App.Models;
using PhysicalKBLayoutSwitcher.App.Services;
using PhysicalKBLayoutSwitcher.App.Win32;

namespace PhysicalKBLayoutSwitcher.App;

public sealed class MainForm : Form
{
    private const string AppTitle = "Physical KB Layout Switcher";

    private readonly AutoStartService autoStartService = new();
    private readonly ConfigurationStore configurationStore = new();
    private readonly RawKeyboardListener keyboardListener = new();
    private readonly KeyboardLayoutCatalog keyboardLayoutCatalog = new();
    private readonly KeyboardLayoutSwitcher keyboardLayoutSwitcher = new();
    private readonly HashSet<string> unmappedActiveDevices = [];
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip trayMenu;
    private readonly ToolStripMenuItem statusMenuItem;
    private readonly ToolStripMenuItem controlCenterMenuItem;
    private readonly ToolStripMenuItem refreshDevicesMenuItem;
    private readonly ToolStripMenuItem debugMonitorMenuItem;
    private readonly ToolStripMenuItem autoStartMenuItem;
    private readonly ToolStripMenuItem manageMappingsMenuItem;
    private readonly ToolStripMenuItem configPathMenuItem;
    private readonly ToolStripMenuItem exitMenuItem;

    private AppConfiguration configuration;
    private ControlCenterForm? controlCenterForm;
    private DebugMonitorForm? debugMonitorForm;
    private DeviceMappingsForm? deviceMappingsForm;

    public MainForm()
    {
        configuration = configurationStore.Load();
        RemoveNoiseMappings();
        EnsureAutoStartEnabled();
        keyboardListener.KeyboardInputReceived += KeyboardListener_KeyboardInputReceived;
        keyboardListener.DevicesChanged += KeyboardListener_DevicesChanged;

        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        Opacity = 0;

        statusMenuItem = new ToolStripMenuItem();
        controlCenterMenuItem = new ToolStripMenuItem("Open Control Center");
        refreshDevicesMenuItem = new ToolStripMenuItem("Refresh Devices");
        debugMonitorMenuItem = new ToolStripMenuItem("Open Debug Monitor");
        autoStartMenuItem = new ToolStripMenuItem("Launch at Windows Sign-In");
        manageMappingsMenuItem = new ToolStripMenuItem("Manage Layout Mappings");
        configPathMenuItem = new ToolStripMenuItem("Open Config Folder");
        exitMenuItem = new ToolStripMenuItem("Exit");

        trayMenu = new ContextMenuStrip();
        trayMenu.Items.AddRange([statusMenuItem, controlCenterMenuItem, refreshDevicesMenuItem, debugMonitorMenuItem, autoStartMenuItem, manageMappingsMenuItem, configPathMenuItem, exitMenuItem]);

        notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "Physical KB Layout Switcher",
            Icon = SystemIcons.Application,
            ContextMenuStrip = trayMenu,
        };

        notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
        controlCenterMenuItem.Click += ControlCenterMenuItem_Click;
        refreshDevicesMenuItem.Click += RefreshDevicesMenuItem_Click;
        debugMonitorMenuItem.Click += DebugMonitorMenuItem_Click;
        autoStartMenuItem.Click += AutoStartMenuItem_Click;
        manageMappingsMenuItem.Click += ManageMappingsMenuItem_Click;
        configPathMenuItem.Click += ConfigPathMenuItem_Click;
        exitMenuItem.Click += ExitMenuItem_Click;

        UpdateAutoStartMenuState();
        UpdateStatusText();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        keyboardListener.Attach(Handle);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Hide();
        BeginInvoke((Action)OpenControlCenter);
    }

    protected override void WndProc(ref Message m)
    {
        if (keyboardListener.ProcessWindowMessage(m))
        {
            return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            controlCenterForm?.Dispose();
            debugMonitorForm?.Dispose();
            notifyIcon.Dispose();
            trayMenu.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ControlCenterMenuItem_Click(object? sender, EventArgs e)
    {
        OpenControlCenter();
    }

    private void DebugMonitorMenuItem_Click(object? sender, EventArgs e)
    {
        if (debugMonitorForm is null || debugMonitorForm.IsDisposed)
        {
            debugMonitorForm = new DebugMonitorForm();
            debugMonitorForm.FormClosed += (_, _) => debugMonitorForm = null;
            debugMonitorForm.SetDevices(keyboardListener.KnownDevices);
        }

        debugMonitorForm.Show();
        debugMonitorForm.BringToFront();
    }

    private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
    {
        OpenControlCenter();
    }

    private void ConfigPathMenuItem_Click(object? sender, EventArgs e)
    {
        var configDirectory = Path.GetDirectoryName(configurationStore.ConfigFilePath);
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = configDirectory,
            UseShellExecute = true,
        });
    }

    private void ExitMenuItem_Click(object? sender, EventArgs e)
    {
        controlCenterForm?.Hide();
        Close();
    }

    private void ManageMappingsMenuItem_Click(object? sender, EventArgs e)
    {
        OpenMappingsDialog();
    }

    private void RefreshDevicesMenuItem_Click(object? sender, EventArgs e)
    {
        RefreshDevices();
    }

    private void AutoStartMenuItem_Click(object? sender, EventArgs e)
    {
        var shouldEnable = !configuration.AutoStartEnabled;

        try
        {
            autoStartService.SetEnabled(shouldEnable);
            configuration.AutoStartEnabled = shouldEnable;
            configurationStore.Save(configuration);
            AppLog.Info($"Auto-start {(shouldEnable ? "enabled" : "disabled")}.");
            UpdateAutoStartMenuState();
            UpdateControlCenterState();
        }
        catch (Exception exception)
        {
            AppLog.Error("Failed to update auto-start.", exception);
            MessageBox.Show(
                $"Could not update auto-start. A log was written to:{Environment.NewLine}{AppLog.LogPath}",
                AppTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void KeyboardListener_DevicesChanged(object? sender, EventArgs e)
    {
        debugMonitorForm?.SetDevices(keyboardListener.KnownDevices);
        UpdateStatusText();
        UpdateControlCenterState();
    }

    private void KeyboardListener_KeyboardInputReceived(object? sender, KeyboardInputEvent inputEvent)
    {
        debugMonitorForm?.SetDevices(keyboardListener.KnownDevices);
        debugMonitorForm?.AddEvent(inputEvent);
        deviceMappingsForm?.NoteKeyboardActivity(inputEvent.DeviceName);
        UpdateStatusText();
        UpdateControlCenterState();

        if (!inputEvent.IsKeyDown)
        {
            return;
        }

        var deviceId = inputEvent.DeviceName;
        var mappedLayoutId = configuration.GetLayoutForDevice(deviceId);
        if (string.IsNullOrWhiteSpace(mappedLayoutId))
        {
            if (unmappedActiveDevices.Add(deviceId))
            {
                AppLog.Info($"Saw active unmapped device '{deviceId}'.");
                UpdateStatusText();
            }

            return;
        }

        try
        {
            var switched = keyboardLayoutSwitcher.TrySwitchToLayout(mappedLayoutId);
            if (!switched)
            {
                AppLog.Info($"Layout switch skipped or failed for device '{deviceId}' -> '{mappedLayoutId}'.");
            }
        }
        catch (Exception exception)
        {
            AppLog.Error($"Layout switch threw for device '{deviceId}' -> '{mappedLayoutId}'.", exception);
        }
    }

    private void UpdateStatusText()
    {
        var mappingCount = configuration.DeviceMappings.Count;
        var knownDevices = keyboardListener.KnownDevices;
        var connectedDeviceCount = knownDevices.Count(device => device.IsConnected);
        var seenDeviceCount = knownDevices.Count;
        var unmappedCount = unmappedActiveDevices.Count;
        statusMenuItem.Enabled = false;
        statusMenuItem.Text = $"Connected: {connectedDeviceCount} | Known: {seenDeviceCount} | Saved mappings: {mappingCount} | Unmapped active: {unmappedCount}";
        manageMappingsMenuItem.Text = unmappedCount > 0
            ? $"Manage Layout Mappings ({unmappedCount} unmapped)"
            : "Manage Layout Mappings";
    }

    private void OpenControlCenter()
    {
        if (controlCenterForm is not null && !controlCenterForm.IsDisposed)
        {
            controlCenterForm.Show();
            controlCenterForm.WindowState = FormWindowState.Normal;
            controlCenterForm.BringToFront();
            controlCenterForm.Activate();
            return;
        }

        try
        {
            AppLog.Info("Opening control center.");
            controlCenterForm = new ControlCenterForm(
                keyboardLayoutCatalog.GetAvailableLayouts(),
                RefreshDevices,
                OpenMappings,
                OpenDebugMonitor,
                ToggleAutoStart,
                OpenConfigFolder,
                ExitApplication);
            controlCenterForm.FormClosed += ControlCenterForm_FormClosed;
            controlCenterForm.SetState(configuration, keyboardListener.KnownDevices, unmappedActiveDevices.Count, configurationStore.ConfigFilePath);
            controlCenterForm.Show();
            controlCenterForm.BringToFront();
            controlCenterForm.Activate();
        }
        catch (Exception exception)
        {
            AppLog.Error("Control center failed.", exception);
            MessageBox.Show(
                $"The control center hit an error. A log was written to:{Environment.NewLine}{AppLog.LogPath}",
                AppTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RefreshDevices()
    {
        try
        {
            keyboardListener.RefreshDevices();
            UpdateStatusText();
            UpdateControlCenterState();
        }
        catch (Exception exception)
        {
            AppLog.Error("Device refresh failed.", exception);
            MessageBox.Show(
                $"The device refresh hit an error. A log was written to:{Environment.NewLine}{AppLog.LogPath}",
                AppTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ToggleAutoStart()
    {
        AutoStartMenuItem_Click(this, EventArgs.Empty);
    }

    private void OpenDebugMonitor()
    {
        DebugMonitorMenuItem_Click(this, EventArgs.Empty);
    }

    private void OpenMappings()
    {
        OpenMappingsDialog();
    }

    private void OpenConfigFolder()
    {
        ConfigPathMenuItem_Click(this, EventArgs.Empty);
    }

    private void ExitApplication()
    {
        ExitMenuItem_Click(this, EventArgs.Empty);
    }

    private void UpdateControlCenterState()
    {
        if (controlCenterForm is null || controlCenterForm.IsDisposed)
        {
            return;
        }

        controlCenterForm.SetState(
            configuration,
            keyboardListener.KnownDevices,
            unmappedActiveDevices.Count,
            configurationStore.ConfigFilePath);
    }

    private void OpenMappingsDialog(string? deviceIdToSelect = null)
    {
        if (deviceMappingsForm is not null && !deviceMappingsForm.IsDisposed)
        {
            if (!string.IsNullOrWhiteSpace(deviceIdToSelect))
            {
                deviceMappingsForm.SelectDevice(deviceIdToSelect);
            }

            deviceMappingsForm.Show();
            deviceMappingsForm.BringToFront();
            return;
        }

        var layouts = keyboardLayoutCatalog.GetAvailableLayouts();
        if (layouts.Count == 0)
        {
            MessageBox.Show(
                "No keyboard layouts are currently loaded for Windows to switch to. Add the layouts you want in Windows first, then try again.",
                "Physical KB Layout Switcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            AppLog.Info($"Opening mappings dialog. Device preselect: '{deviceIdToSelect ?? "<none>"}'.");
            deviceMappingsForm = new DeviceMappingsForm(keyboardListener.KnownDevices, configuration, layouts);
            deviceMappingsForm.FormClosed += DeviceMappingsForm_FormClosed;
            deviceMappingsForm.MappingsChanged += DeviceMappingsForm_MappingsChanged;
            if (!string.IsNullOrWhiteSpace(deviceIdToSelect))
            {
                deviceMappingsForm.Shown += (_, _) => deviceMappingsForm.SelectDevice(deviceIdToSelect);
            }

            deviceMappingsForm.Show();
            deviceMappingsForm.BringToFront();
        }
        catch (Exception exception)
        {
            AppLog.Error("Mappings dialog failed.", exception);
            MessageBox.Show(
                $"The mappings dialog hit an error. A log was written to:{Environment.NewLine}{AppLog.LogPath}",
                "Physical KB Layout Switcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RemoveNoiseMappings()
    {
        var removed = configuration.DeviceMappings.RemoveAll(mapping =>
            DeviceIdentity.IsLikelyNoiseDevice(mapping.DeviceId));

        if (removed <= 0)
        {
            return;
        }

        configurationStore.Save(configuration);
        AppLog.Info($"Removed {removed} virtual/noise device mappings from config.");
    }

    private void EnsureAutoStartEnabled()
    {
        if (!configuration.AutoStartPreferenceInitialized)
        {
            try
            {
                configuration.AutoStartPreferenceInitialized = true;
                configuration.AutoStartEnabled = true;
                autoStartService.SetEnabled(true);
                configurationStore.Save(configuration);
                AppLog.Info("Auto-start enabled.");
            }
            catch (Exception exception)
            {
                AppLog.Error("Failed to enable auto-start during startup.", exception);
            }

            return;
        }

        try
        {
            autoStartService.SetEnabled(configuration.AutoStartEnabled);
        }
        catch (Exception exception)
        {
            AppLog.Error("Failed to sync auto-start during startup.", exception);
        }
    }

    private void UpdateAutoStartMenuState()
    {
        autoStartMenuItem.Checked = configuration.AutoStartEnabled;
    }

    private void DeviceMappingsForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        if (deviceMappingsForm is not null)
        {
            deviceMappingsForm.MappingsChanged -= DeviceMappingsForm_MappingsChanged;
            deviceMappingsForm.FormClosed -= DeviceMappingsForm_FormClosed;
        }

        deviceMappingsForm = null;
    }

    private void DeviceMappingsForm_MappingsChanged(object? sender, DeviceMappingsChangedEventArgs e)
    {
        configuration = e.Configuration;
        configurationStore.Save(configuration);
        AppLog.Info($"Saved {configuration.DeviceMappings.Count} device mappings.");
        unmappedActiveDevices.RemoveWhere(deviceId =>
            !string.IsNullOrWhiteSpace(configuration.GetLayoutForDevice(deviceId)));
        UpdateStatusText();
        UpdateControlCenterState();
    }

    private void ControlCenterForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        if (controlCenterForm is not null)
        {
            controlCenterForm.FormClosed -= ControlCenterForm_FormClosed;
        }

        controlCenterForm = null;
    }
}
