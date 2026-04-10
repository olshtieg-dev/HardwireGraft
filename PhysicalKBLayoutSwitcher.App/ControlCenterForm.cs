using PhysicalKBLayoutSwitcher.App.Models;
using PhysicalKBLayoutSwitcher.App.Services;

namespace PhysicalKBLayoutSwitcher.App;

public sealed class ControlCenterForm : Form
{
    private readonly IReadOnlyList<KeyboardLayoutOption> layouts;
    private readonly Action refreshDevicesAction;
    private readonly Action openMappingsAction;
    private readonly Action openDebugMonitorAction;
    private readonly Action toggleAutoStartAction;
    private readonly Action openConfigFolderAction;
    private readonly Action exitAction;
    private readonly ListView devicesListView;
    private readonly Label summaryValueLabel;
    private readonly Label autoStartValueLabel;
    private readonly Label configPathValueLabel;
    private readonly Label hintValueLabel;
    private readonly Button refreshButton;
    private readonly Button manageMappingsButton;
    private readonly Button debugMonitorButton;
    private readonly Button autoStartButton;
    private readonly Button openConfigButton;
    private readonly Button exitButton;

    private AppConfiguration currentConfiguration = new();
    private IReadOnlyCollection<KeyboardDeviceSnapshot> currentDevices = [];
    private int unmappedActiveCount;
    private string configPath = string.Empty;

    public ControlCenterForm(
        IReadOnlyList<KeyboardLayoutOption> layouts,
        Action refreshDevicesAction,
        Action openMappingsAction,
        Action openDebugMonitorAction,
        Action toggleAutoStartAction,
        Action openConfigFolderAction,
        Action exitAction)
    {
        this.layouts = layouts;
        this.refreshDevicesAction = refreshDevicesAction;
        this.openMappingsAction = openMappingsAction;
        this.openDebugMonitorAction = openDebugMonitorAction;
        this.toggleAutoStartAction = toggleAutoStartAction;
        this.openConfigFolderAction = openConfigFolderAction;
        this.exitAction = exitAction;

        Text = "Physical KB Layout Switcher";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1020, 620);
        ShowInTaskbar = true;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        BackColor = SystemColors.Window;

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Text = "Physical KB Layout Switcher",
        };

        hintValueLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Text = "This app stays in the tray while it watches keyboard input. Use Refresh Devices after plugging in a keyboard, or open Mappings to change assignments.",
        };

        summaryValueLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
        };

        autoStartValueLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
        };

        configPathValueLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
        };

        devicesListView = new ListView
        {
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            MultiSelect = false,
            View = View.Details,
        };
        devicesListView.Columns.Add("Status", 90);
        devicesListView.Columns.Add("Device", 410);
        devicesListView.Columns.Add("Mapped Layout", 220);
        devicesListView.Columns.Add("Events", 80);
        devicesListView.Columns.Add("Last Seen", 130);

        refreshButton = new Button
        {
            AutoSize = true,
            Text = "Refresh Devices",
        };
        refreshButton.Click += (_, _) => refreshDevicesAction();

        manageMappingsButton = new Button
        {
            AutoSize = true,
            Text = "Manage Mappings",
        };
        manageMappingsButton.Click += (_, _) => openMappingsAction();

        debugMonitorButton = new Button
        {
            AutoSize = true,
            Text = "Open Debug Monitor",
        };
        debugMonitorButton.Click += (_, _) => openDebugMonitorAction();

        autoStartButton = new Button
        {
            AutoSize = true,
            Text = "Toggle Auto-Start",
        };
        autoStartButton.Click += (_, _) => toggleAutoStartAction();

        openConfigButton = new Button
        {
            AutoSize = true,
            Text = "Open Config Folder",
        };
        openConfigButton.Click += (_, _) => openConfigFolderAction();

        exitButton = new Button
        {
            AutoSize = true,
            Text = "Exit App",
        };
        exitButton.Click += (_, _) => exitAction();

        var buttonStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
        };
        buttonStrip.Controls.Add(refreshButton);
        buttonStrip.Controls.Add(manageMappingsButton);
        buttonStrip.Controls.Add(debugMonitorButton);
        buttonStrip.Controls.Add(autoStartButton);
        buttonStrip.Controls.Add(openConfigButton);
        buttonStrip.Controls.Add(exitButton);

        var detailsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            Padding = new Padding(12),
        };
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.Controls.Add(titleLabel, 0, 0);
        detailsPanel.Controls.Add(hintValueLabel, 0, 1);
        detailsPanel.Controls.Add(new Label { AutoSize = true, Text = "Status" }, 0, 2);
        detailsPanel.Controls.Add(summaryValueLabel, 0, 3);
        detailsPanel.Controls.Add(new Label { AutoSize = true, Text = "Auto-Start" }, 0, 4);
        detailsPanel.Controls.Add(autoStartValueLabel, 0, 5);
        detailsPanel.Controls.Add(new Label { AutoSize = true, Text = "Config Folder" }, 0, 6);
        detailsPanel.Controls.Add(configPathValueLabel, 0, 7);

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 640,
        };
        splitContainer.Panel1.Controls.Add(devicesListView);
        splitContainer.Panel2.Controls.Add(detailsPanel);
        splitContainer.Panel2.Controls.Add(buttonStrip);

        Controls.Add(splitContainer);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    public void SetState(
        AppConfiguration configuration,
        IReadOnlyCollection<KeyboardDeviceSnapshot> devices,
        int unmappedActiveCount,
        string configPath)
    {
        currentConfiguration = configuration.Clone();
        currentDevices = devices.ToArray();
        this.unmappedActiveCount = unmappedActiveCount;
        this.configPath = configPath;

        summaryValueLabel.Text =
            $"Connected: {currentDevices.Count(device => device.IsConnected)} | Known: {currentDevices.Count} | Saved mappings: {currentConfiguration.DeviceMappings.Count} | Unmapped active: {this.unmappedActiveCount}";
        autoStartValueLabel.Text = currentConfiguration.AutoStartEnabled
            ? "Enabled"
            : "Disabled";
        configPathValueLabel.Text = configPath;
        autoStartButton.Text = currentConfiguration.AutoStartEnabled
            ? "Disable Auto-Start"
            : "Enable Auto-Start";

        PopulateDeviceList();
    }

    private void PopulateDeviceList()
    {
        devicesListView.BeginUpdate();
        devicesListView.Items.Clear();

        foreach (var device in currentDevices
                     .OrderBy(device => device.IsConnected ? 0 : 1)
                     .ThenBy(device => DeviceIdentity.GetDisplayName(device.DeviceName), StringComparer.CurrentCultureIgnoreCase))
        {
            var item = new ListViewItem(device.IsConnected ? "Online" : "Offline");
            item.SubItems.Add(DeviceIdentity.GetDisplayName(device.DeviceName));
            item.SubItems.Add(GetLayoutDisplayText(currentConfiguration.GetLayoutForDevice(device.DeviceName)));
            item.SubItems.Add(device.EventCount.ToString());
            item.SubItems.Add(device.LastSeenAt == default ? string.Empty : device.LastSeenAt.LocalDateTime.ToString("T"));
            item.Tag = device.DeviceName;
            devicesListView.Items.Add(item);
        }

        devicesListView.EndUpdate();
    }

    private string GetLayoutDisplayText(string? layoutId)
    {
        if (string.IsNullOrWhiteSpace(layoutId))
        {
            return "(No mapping)";
        }

        var layout = layouts.FirstOrDefault(entry =>
            string.Equals(entry.LayoutId, layoutId, StringComparison.OrdinalIgnoreCase));

        return layout?.ToString() ?? layoutId;
    }
}
