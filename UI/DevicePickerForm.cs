using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using OPods.Pods;

namespace OPods.UI;

/// <summary>
/// Scans for nearby Bluetooth devices and lets the user pick one to connect.
/// Returns the selected <see cref="BluetoothAddress"/> and connection method
/// via <see cref="SelectedAddress"/> / <see cref="SelectedMethod"/>.
/// </summary>
public partial class DevicePickerForm : Form
{
    private readonly List<BluetoothDeviceInfo> _devices = new();

    public BluetoothAddress? SelectedAddress { get; private set; }
    public string SelectedName { get; private set; } = string.Empty;
    public RfcommConnectionMethod SelectedMethod { get; private set; } = RfcommConnectionMethod.Uuid;

    public DevicePickerForm()
    {
        InitializeComponent();
        connectionMethodCombo.SelectedIndex =
            RfcommConnectionMethodExtensions.SelectedIndexOf(
                RfcommConnectionMethodExtensions.FromPreference(Preferences.RfcommConnectionMethod));
    }

    private async void ScanButton_Click(object? sender, EventArgs e)
    {
        scanButton.Enabled = false;
        scanProgress.Visible = true;
        statusLabel.Text = "正在扫描附近蓝牙设备…";
        deviceList.Items.Clear();
        _devices.Clear();
        connectButton.Enabled = false;

        try
        {
            var devices = await Task.Run(() =>
            {
                using var client = new BluetoothClient();
                return client.DiscoverDevices().ToArray();
            }).ConfigureAwait(true);

            _devices.AddRange(devices);
            foreach (var dev in devices)
            {
                var item = new ListViewItem(dev.DeviceName ?? "(未知设备)");
                item.SubItems.Add(dev.DeviceAddress.ToString());
                item.SubItems.Add(dev.Authenticated ? "是" : "否");
                item.SubItems.Add(dev.Connected ? "是" : "否");
                deviceList.Items.Add(item);
            }
            statusLabel.Text = $"扫描完成，发现 {devices.Length} 个设备";
        }
        catch (Exception ex)
        {
            statusLabel.Text = "扫描失败：" + ex.Message;
        }
        finally
        {
            scanProgress.Visible = false;
            scanButton.Enabled = true;
        }
    }

    private void DeviceList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        connectButton.Enabled = deviceList.SelectedIndices.Count > 0;
    }

    private void ConnectButton_Click(object? sender, EventArgs e)
    {
        if (deviceList.SelectedIndices.Count == 0) return;
        int idx = deviceList.SelectedIndices[0];
        if (idx < 0 || idx >= _devices.Count) return;

        var dev = _devices[idx];
        SelectedAddress = dev.DeviceAddress;
        SelectedName = dev.DeviceName ?? dev.DeviceAddress.ToString();
        SelectedMethod = RfcommConnectionMethodExtensions.FromSelectedIndex(connectionMethodCombo.SelectedIndex);

        Preferences.LastDeviceAddress = dev.DeviceAddress.ToString();
        Preferences.LastDeviceName = SelectedName;
        Preferences.RfcommConnectionMethod = SelectedMethod.PreferenceValue();
        Preferences.Save();

        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
