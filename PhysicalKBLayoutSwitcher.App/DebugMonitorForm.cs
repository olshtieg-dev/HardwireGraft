using PhysicalKBLayoutSwitcher.App.Models;

namespace PhysicalKBLayoutSwitcher.App;

public sealed class DebugMonitorForm : Form
{
    private readonly ListView devicesListView;
    private readonly ListBox eventsListBox;

    public DebugMonitorForm()
    {
        Text = "Physical KB Layout Switcher Debug Monitor";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 520);

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 220,
        };

        devicesListView = new ListView
        {
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            GridLines = true,
            View = View.Details,
        };
        devicesListView.Columns.Add("Device Handle", 180);
        devicesListView.Columns.Add("Status", 80);
        devicesListView.Columns.Add("Device Name", 360);
        devicesListView.Columns.Add("Events", 80);
        devicesListView.Columns.Add("Last Seen", 120);
        devicesListView.Columns.Add("Last Key", 120);

        eventsListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true,
        };

        splitContainer.Panel1.Controls.Add(devicesListView);
        splitContainer.Panel2.Controls.Add(eventsListBox);
        Controls.Add(splitContainer);
    }

    public void SetDevices(IEnumerable<KeyboardDeviceSnapshot> devices)
    {
        devicesListView.BeginUpdate();
        devicesListView.Items.Clear();

        foreach (var device in devices.OrderBy(device => device.DeviceName, StringComparer.OrdinalIgnoreCase))
        {
            var item = new ListViewItem(device.DeviceHandle);
            item.SubItems.Add(device.IsConnected ? "Online" : "Offline");
            item.SubItems.Add(device.DeviceName);
            item.SubItems.Add(device.EventCount.ToString());
            item.SubItems.Add(device.LastSeenAt == default ? string.Empty : device.LastSeenAt.LocalDateTime.ToString("T"));
            item.SubItems.Add(device.LastVirtualKeyName ?? string.Empty);
            devicesListView.Items.Add(item);
        }

        devicesListView.EndUpdate();
    }

    public void AddEvent(KeyboardInputEvent inputEvent)
    {
        var entry =
            $"{inputEvent.SeenAt.LocalDateTime:T} | {(inputEvent.IsKeyDown ? "Down" : "Up  ")} | {inputEvent.VirtualKeyName} (0x{inputEvent.VirtualKey:X2}) | {inputEvent.MessageName} | MAKE 0x{inputEvent.MakeCode:X2} | FLAGS 0x{inputEvent.Flags:X2} | {inputEvent.DeviceName}";

        eventsListBox.Items.Insert(0, entry);
        while (eventsListBox.Items.Count > 200)
        {
            eventsListBox.Items.RemoveAt(eventsListBox.Items.Count - 1);
        }
    }
}
