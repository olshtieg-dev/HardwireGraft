using PhysicalKBLayoutSwitcher.App.Models;
using PhysicalKBLayoutSwitcher.App.Services;

namespace PhysicalKBLayoutSwitcher.App;

public sealed class DeviceMappingsForm : Form
{
    private static readonly KeyboardLayoutOption NoMappingOption = new()
    {
        LayoutId = string.Empty,
        DisplayName = "(No mapping)",
    };

    private readonly AppConfiguration workingConfiguration;
    private readonly IReadOnlyList<KeyboardLayoutOption> layouts;
    private readonly ListView devicesListView;
    private readonly ComboBox layoutComboBox;
    private readonly Label selectedDeviceLabel;
    private readonly Label selectedLayoutLabel;
    private readonly Label selectedStatusLabel;
    private readonly Label liveCaptureLabel;
    private readonly Button applyButton;
    private readonly Button clearButton;
    private readonly Button closeButton;
    private readonly Dictionary<string, DeviceEntry> deviceEntries;

    private bool hasChanges;

    public event EventHandler<DeviceMappingsChangedEventArgs>? MappingsChanged;

    public DeviceMappingsForm(
        IEnumerable<KeyboardDeviceSnapshot> knownDevices,
        AppConfiguration configuration,
        IReadOnlyList<KeyboardLayoutOption> layouts)
    {
        this.layouts = layouts;
        workingConfiguration = configuration.Clone();
        deviceEntries = BuildDeviceEntries(knownDevices, workingConfiguration);

        Text = "Keyboard Layout Mappings";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 480);
        ShowInTaskbar = true;
        TopMost = true;

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel2,
            SplitterDistance = 620,
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
        devicesListView.Columns.Add("Device", 390);
        devicesListView.Columns.Add("Mapped Layout", 260);
        devicesListView.Columns.Add("Handle", 120);
        devicesListView.SelectedIndexChanged += DevicesListView_SelectedIndexChanged;

        selectedDeviceLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(260, 0),
        };

        selectedStatusLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(260, 0),
        };

        selectedLayoutLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(260, 0),
        };

        liveCaptureLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(260, 0),
            Text = "Press a key on the keyboard you want to identify. This window will auto-select the matching device.",
        };

        layoutComboBox = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FormattingEnabled = true,
        };
        layoutComboBox.Items.Add(NoMappingOption);
        foreach (var layout in layouts)
        {
            layoutComboBox.Items.Add(layout);
        }

        applyButton = new Button
        {
            AutoSize = true,
            Text = "Apply Mapping",
        };
        applyButton.Click += ApplyButton_Click;

        clearButton = new Button
        {
            AutoSize = true,
            Text = "Clear Mapping",
        };
        clearButton.Click += ClearButton_Click;

        closeButton = new Button
        {
            AutoSize = true,
            Text = "Close",
        };
        closeButton.Click += CloseButton_Click;

        var instructionsLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(260, 0),
            Text = "Pick a device, choose one of the keyboard layouts Windows already has loaded, then apply the mapping.",
        };

        var detailsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 10,
            Padding = new Padding(12),
        };
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle());
        detailsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        detailsPanel.Controls.Add(instructionsLabel, 0, 0);
        detailsPanel.Controls.Add(liveCaptureLabel, 0, 1);
        detailsPanel.Controls.Add(new Label { AutoSize = true, Text = "Selected Device" }, 0, 2);
        detailsPanel.Controls.Add(selectedDeviceLabel, 0, 3);
        detailsPanel.Controls.Add(new Label { AutoSize = true, Text = "Status" }, 0, 4);
        detailsPanel.Controls.Add(selectedStatusLabel, 0, 5);
        detailsPanel.Controls.Add(new Label { AutoSize = true, Text = "Current Mapping" }, 0, 6);
        detailsPanel.Controls.Add(selectedLayoutLabel, 0, 7);
        detailsPanel.Controls.Add(layoutComboBox, 0, 8);

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
        };
        buttonsPanel.Controls.Add(applyButton);
        buttonsPanel.Controls.Add(clearButton);
        buttonsPanel.Controls.Add(closeButton);

        splitContainer.Panel1.Controls.Add(devicesListView);
        splitContainer.Panel2.Controls.Add(detailsPanel);
        splitContainer.Panel2.Controls.Add(buttonsPanel);
        Controls.Add(splitContainer);

        PopulateDeviceList();
        UpdateSelectionDetails();
    }

    public AppConfiguration UpdatedConfiguration => workingConfiguration;

    public bool HasChanges => hasChanges;

    public void SelectDevice(string deviceId)
    {
        foreach (ListViewItem item in devicesListView.Items)
        {
            if (!string.Equals(item.Tag as string, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            item.Selected = true;
            item.Focused = true;
            item.EnsureVisible();
            liveCaptureLabel.Text = $"Last keyboard activity matched: {item.SubItems[1].Text}";
            return;
        }
    }

    public void NoteKeyboardActivity(string deviceId)
    {
        SelectDevice(deviceId);
    }

    private static Dictionary<string, DeviceEntry> BuildDeviceEntries(
        IEnumerable<KeyboardDeviceSnapshot> knownDevices,
        AppConfiguration configuration)
    {
        var entries = new Dictionary<string, DeviceEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var device in knownDevices)
        {
            if (!ShouldIncludeDevice(device, configuration))
            {
                continue;
            }

            entries[device.DeviceName] = new DeviceEntry
            {
                DeviceId = device.DeviceName,
                DeviceDisplayName = DeviceIdentity.GetDisplayName(device.DeviceName),
                DeviceHandle = device.DeviceHandle,
                Status = device.IsConnected ? "Online" : "Offline",
            };
        }

        foreach (var mapping in configuration.DeviceMappings)
        {
            if (DeviceIdentity.IsLikelyNoiseDevice(mapping.DeviceId))
            {
                continue;
            }

            if (entries.ContainsKey(mapping.DeviceId))
            {
                continue;
            }

            entries[mapping.DeviceId] = new DeviceEntry
            {
                DeviceId = mapping.DeviceId,
                DeviceDisplayName = string.IsNullOrWhiteSpace(mapping.DeviceName) ? mapping.DeviceId : mapping.DeviceName,
                DeviceHandle = string.Empty,
                Status = "Saved only",
            };
        }

        return entries;
    }

    private static bool ShouldIncludeDevice(KeyboardDeviceSnapshot device, AppConfiguration configuration)
    {
        var isMapped = !string.IsNullOrWhiteSpace(configuration.GetLayoutForDevice(device.DeviceName));
        if (isMapped)
        {
            return true;
        }

        if (device.EventCount <= 0)
        {
            return false;
        }

        return !DeviceIdentity.IsLikelyNoiseDevice(device.DeviceName);
    }

    private void PopulateDeviceList()
    {
        devicesListView.BeginUpdate();
        devicesListView.Items.Clear();

        foreach (var entry in deviceEntries.Values.OrderBy(entry => entry.DeviceDisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            var mappedLayoutId = workingConfiguration.GetLayoutForDevice(entry.DeviceId);
            var mappedLayoutText = GetLayoutDisplayText(mappedLayoutId);

            var item = new ListViewItem(entry.Status);
            item.SubItems.Add(entry.DeviceDisplayName);
            item.SubItems.Add(mappedLayoutText);
            item.SubItems.Add(entry.DeviceHandle);
            item.Tag = entry.DeviceId;
            devicesListView.Items.Add(item);
        }

        devicesListView.EndUpdate();

        if (devicesListView.Items.Count > 0 && devicesListView.SelectedItems.Count == 0)
        {
            devicesListView.Items[0].Selected = true;
        }
    }

    private void DevicesListView_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateSelectionDetails();
    }

    private void UpdateSelectionDetails()
    {
        var selectedEntry = GetSelectedEntry();
        var hasSelection = selectedEntry is not null;

        selectedDeviceLabel.Text = selectedEntry?.DeviceDisplayName ?? "Select a device from the list.";
        selectedStatusLabel.Text = selectedEntry?.Status ?? string.Empty;
        selectedLayoutLabel.Text = GetLayoutDisplayText(selectedEntry is null
            ? null
            : workingConfiguration.GetLayoutForDevice(selectedEntry.DeviceId));

        layoutComboBox.Enabled = hasSelection && layoutComboBox.Items.Count > 0;
        applyButton.Enabled = hasSelection && layouts.Count > 0;
        clearButton.Enabled = hasSelection && !string.IsNullOrWhiteSpace(selectedEntry is null
            ? null
            : workingConfiguration.GetLayoutForDevice(selectedEntry.DeviceId));

        if (!hasSelection)
        {
            layoutComboBox.SelectedItem = NoMappingOption;
            return;
        }

        var mappedLayoutId = workingConfiguration.GetLayoutForDevice(selectedEntry!.DeviceId);
        layoutComboBox.SelectedItem = FindLayoutOption(mappedLayoutId) ?? NoMappingOption;
    }

    private void ApplyButton_Click(object? sender, EventArgs e)
    {
        var selectedEntry = GetSelectedEntry();
        if (selectedEntry is null)
        {
            return;
        }

        var selectedLayout = layoutComboBox.SelectedItem as KeyboardLayoutOption;
        if (selectedLayout is null || string.IsNullOrWhiteSpace(selectedLayout.LayoutId))
        {
            workingConfiguration.RemoveLayoutForDevice(selectedEntry.DeviceId);
        }
        else
        {
            workingConfiguration.SetLayoutForDevice(
                selectedEntry.DeviceId,
                selectedLayout.LayoutId,
                selectedEntry.DeviceDisplayName);
        }

        hasChanges = true;
        PopulateDeviceList();
        SelectDevice(selectedEntry.DeviceId);
        UpdateSelectionDetails();
        MappingsChanged?.Invoke(this, new DeviceMappingsChangedEventArgs(workingConfiguration.Clone()));
    }

    private void ClearButton_Click(object? sender, EventArgs e)
    {
        layoutComboBox.SelectedItem = NoMappingOption;
        ApplyButton_Click(sender, e);
    }

    private void CloseButton_Click(object? sender, EventArgs e)
    {
        DialogResult = hasChanges ? DialogResult.OK : DialogResult.Cancel;
        Close();
    }

    private DeviceEntry? GetSelectedEntry()
    {
        if (devicesListView.SelectedItems.Count == 0)
        {
            return null;
        }

        var deviceId = devicesListView.SelectedItems[0].Tag as string;
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return deviceEntries.GetValueOrDefault(deviceId);
    }

    private KeyboardLayoutOption? FindLayoutOption(string? layoutId)
    {
        if (string.IsNullOrWhiteSpace(layoutId))
        {
            return null;
        }

        return layouts.FirstOrDefault(layout =>
            string.Equals(layout.LayoutId, layoutId, StringComparison.OrdinalIgnoreCase));
    }

    private string GetLayoutDisplayText(string? layoutId)
    {
        if (string.IsNullOrWhiteSpace(layoutId))
        {
            return "(No mapping)";
        }

        return FindLayoutOption(layoutId)?.ToString() ?? layoutId;
    }

    private sealed class DeviceEntry
    {
        public required string DeviceId { get; init; }

        public required string DeviceDisplayName { get; init; }

        public required string DeviceHandle { get; init; }

        public required string Status { get; init; }
    }
}

public sealed class DeviceMappingsChangedEventArgs : EventArgs
{
    public DeviceMappingsChangedEventArgs(AppConfiguration configuration)
    {
        Configuration = configuration;
    }

    public AppConfiguration Configuration { get; }
}
