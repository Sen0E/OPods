using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using OPods.Controllers;
using OPods.Pods;

namespace OPods.UI;

/// <summary>
/// Main application window: shows device/battery/ANC/game-mode state and
/// wires user actions to <see cref="PodController"/>.
/// </summary>
public partial class MainForm : Form
{
    private readonly PodController _controller = new();
    private bool _suppressAncEvents;
    private bool _suppressGameModeEvents;

    public MainForm()
    {
        InitializeComponent();
        gameModeImplCombo.SelectedIndex = GameModeImplementationExtensions.SelectedIndexOf(
            GameModeImplementationExtensions.FromPreference(Preferences.GameModeImplementation));

        _controller.StateChanged += OnStateChanged;
        _controller.BatteryChanged += OnBatteryChanged;
        _controller.AncModeChanged += OnAncModeChanged;
        _controller.GameModeChanged += OnGameModeChanged;
        _controller.Log += OnLog;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        UpdateConnectionUi();
        UpdateBatteryUi(_controller.Battery);
        UpdateAncUi(_controller.AncMode);
        UpdateGameModeUi(_controller.GameMode);
        AppendLog("OPods 已启动。点击「更换设备」连接耳机。");
    }

    private async void ChangeDeviceButton_Click(object? sender, EventArgs e)
    {
        if (_controller.State == ConnectionState.Connecting || _controller.State == ConnectionState.Connected)
        {
            await _controller.DisconnectAsync();
        }

        using var picker = new DevicePickerForm();
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedAddress == null) return;

        var impl = GameModeImplementationExtensions.FromSelectedIndex(gameModeImplCombo.SelectedIndex);
        _controller.SetGameModeImplementation(impl);

        try
        {
            await _controller.ConnectAsync(
                picker.SelectedAddress,
                picker.SelectedName,
                picker.SelectedMethod,
                impl);
        }
        catch (OperationCanceledException)
        {
            AppendLog("连接已取消。");
        }
    }

    private async void RefreshButton_Click(object? sender, EventArgs e)
    {
        await _controller.RefreshStatusAsync();
        AppendLog("已请求刷新状态。");
    }

    private async void AncRadio_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressAncEvents) return;
        if (sender is not RadioButton rb || !rb.Checked) return;
        if (_controller.State != ConnectionState.Connected) return;

        var mode = rb == ancOffRadio ? NoiseControlMode.Off
            : rb == ancNoiseCancelRadio ? NoiseControlMode.NoiseCancellation
            : rb == ancAdaptiveRadio ? NoiseControlMode.Adaptive
            : NoiseControlMode.Transparency;

        await _controller.SetAncModeAsync(mode);
    }

    private async void GameModeCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressGameModeEvents) return;
        if (_controller.State != ConnectionState.Connected) return;
        await _controller.SetGameModeAsync(gameModeCheckBox.Checked);
    }

    private void GameModeImplCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var impl = GameModeImplementationExtensions.FromSelectedIndex(gameModeImplCombo.SelectedIndex);
        _controller.SetGameModeImplementation(impl);
        Preferences.GameModeImplementation = impl.PreferenceValue();
        Preferences.Save();
    }

    private void OnStateChanged(object? sender, ConnectionState state)
    {
        if (InvokeRequired) { BeginInvoke(() => OnStateChanged(sender, state)); return; }
        UpdateConnectionUi();
    }

    private void OnBatteryChanged(object? sender, BatteryParams battery)
    {
        if (InvokeRequired) { BeginInvoke(() => OnBatteryChanged(sender, battery)); return; }
        UpdateBatteryUi(battery);
    }

    private void OnAncModeChanged(object? sender, NoiseControlMode mode)
    {
        if (InvokeRequired) { BeginInvoke(() => OnAncModeChanged(sender, mode)); return; }
        UpdateAncUi(mode);
    }

    private void OnGameModeChanged(object? sender, bool enabled)
    {
        if (InvokeRequired) { BeginInvoke(() => OnGameModeChanged(sender, enabled)); return; }
        UpdateGameModeUi(enabled);
    }

    private void OnLog(object? sender, string message)
    {
        if (InvokeRequired) { BeginInvoke(() => OnLog(sender, message)); return; }
        AppendLog(message);
    }

    private void UpdateConnectionUi()
    {
        deviceNameLabel.Text = string.IsNullOrEmpty(_controller.DeviceName) ? "(未连接)" : _controller.DeviceName;
        var (color, text) = _controller.State switch
        {
            ConnectionState.Connected => (Color.Green, "已连接"),
            ConnectionState.Connecting => (Color.Orange, "连接中…"),
            ConnectionState.Error => (Color.Red, "连接错误"),
            _ => (Color.Gray, "未连接")
        };
        statusDotLabel.ForeColor = color;
        statusTextLabel.Text = text;
        refreshButton.Enabled = _controller.State == ConnectionState.Connected;
    }

    private void UpdateBatteryUi(BatteryParams battery)
    {
        leftBatteryLabel.Text = FormatBattery(battery.Left, "左耳");
        rightBatteryLabel.Text = FormatBattery(battery.Right, "右耳");
        caseBatteryLabel.Text = FormatBattery(battery.Case, "充电盒");
    }

    private static string FormatBattery(PodParams? p, string name)
    {
        if (p == null || !p.IsConnected) return $"{name}\n--";
        var charge = p.IsCharging ? " ⚡" : string.Empty;
        return $"{name}\n{p.Battery}%{charge}";
    }

    private void UpdateAncUi(NoiseControlMode mode)
    {
        _suppressAncEvents = true;
        try
        {
            ancOffRadio.Checked = mode == NoiseControlMode.Off;
            ancNoiseCancelRadio.Checked = mode == NoiseControlMode.NoiseCancellation;
            ancAdaptiveRadio.Checked = mode == NoiseControlMode.Adaptive;
            ancTransparencyRadio.Checked = mode == NoiseControlMode.Transparency;
        }
        finally
        {
            _suppressAncEvents = false;
        }
        bool enabled = _controller.State == ConnectionState.Connected;
        ancOffRadio.Enabled = enabled;
        ancNoiseCancelRadio.Enabled = enabled;
        ancAdaptiveRadio.Enabled = enabled;
        ancTransparencyRadio.Enabled = enabled;
    }

    private void UpdateGameModeUi(bool enabled)
    {
        _suppressGameModeEvents = true;
        try
        {
            gameModeCheckBox.Checked = enabled;
        }
        finally
        {
            _suppressGameModeEvents = false;
        }
        gameModeCheckBox.Enabled = _controller.State == ConnectionState.Connected;
    }

    private void AppendLog(string message)
    {
        logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        await _controller.DisconnectAsync();
        _controller.Dispose();
    }
}
