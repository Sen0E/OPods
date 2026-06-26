using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using OPods.Controllers;
using OPods.Pods;

namespace OPods.UI;

/// <summary>
/// Main application window: shows device/battery/ANC/EQ state and wires user
/// actions to <see cref="PodController"/>。协议优先重构后，EQ 可见性由运行时
/// <see cref="PodController.Capabilities"/> 决定；耳机在充电盒中时电量位显示闪电。
/// 游戏模式/空间音频/双设备连接等控件后续移至子窗口。
/// </summary>
public partial class MainForm : Form
{
    private readonly PodController _controller = new();
    private bool _suppressAncEvents;
    private bool _suppressEqEvents;

    private readonly List<RadioButton> _ancMainButtons = new();
    private readonly List<RadioButton> _ancLevelButtons = new();
    private FlowLayoutPanel? _ancSubPanel;
    private DeviceProfile _currentProfile = DeviceProfileRegistry.Default;

    public MainForm()
    {
        InitializeComponent();

        _controller.StateChanged += OnStateChanged;
        _controller.BatteryChanged += OnBatteryChanged;
        _controller.AncModeChanged += OnAncModeChanged;
        _controller.EqPresetChanged += OnEqPresetChanged;
        _controller.CapabilitiesChanged += OnCapabilitiesChanged;
        // 佩戴状态变化需刷新电量显示（在盒中→闪电图标）
        _controller.WearStatusChanged += OnWearStatusChanged;
        _controller.Log += OnLog;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        BuildAncButtons(DeviceProfileRegistry.Default);
        BuildEqUi(DeviceProfileRegistry.Default);
        UpdateConnectionUi();
        UpdateBatteryUi(_controller.Battery);
        UpdateAncUi(_controller.AncMode);
        UpdateEqUi(_controller.EqPresetId);
        AppendLog("OPods 已启动。正在从系统配对表自动检测 OPPO 耳机…");

        _ = AutoDetectAndConnectAsync();
    }

    /// <summary>
    /// 启动时自动检测已连接的 OPPO 耳机。直接读取系统已配对设备列表
    /// （<see cref="BluetoothClient.PairedDevices"/>，不触发无线电扫描，毫秒级返回），
    /// 筛选设备名以 "OPPO Enco" 开头的，优先选择已连接（Connected）的设备。
    /// </summary>
    private async Task AutoDetectAndConnectAsync()
    {
        BluetoothDeviceInfo? target = null;
        try
        {
            AppendLog("正在读取系统已配对的蓝牙设备…");
            BluetoothDeviceInfo[] devices;
            using (var client = new BluetoothClient())
            {
                devices = await Task.Run(() => client.PairedDevices.ToArray()).ConfigureAwait(true);
            }

            var candidates = devices
                .Where(d => !string.IsNullOrEmpty(d.DeviceName) &&
                            d.DeviceName.StartsWith("OPPO Enco", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 0)
            {
                AppendLog("已配对设备中未发现 OPPO Enco 系列耳机。请先在 Windows 蓝牙设置中配对耳机，或点击「更换设备」手动选择。");
                return;
            }

            target = candidates.FirstOrDefault(d => d.Connected)
                  ?? candidates.FirstOrDefault(d => d.Authenticated)
                  ?? candidates[0];

            AppendLog($"检测到 OPPO 耳机：{target.DeviceName}，正在解析机型配置…");
        }
        catch (Exception ex)
        {
            AppendLog($"自动检测失败：{ex.Message}。请点击「更换设备」手动选择。");
            return;
        }

        var profile = DeviceProfileRegistry.Resolve(target.DeviceName);
        AppendLog(profile is GenericOppoProfile
            ? $"未找到 {target.DeviceName} 的专属配置，降级使用通用兜底配置。"
            : $"已识别机型：{profile.ModelName}。");

        await ConnectToAsync(target.DeviceAddress, target.DeviceName, profile.PreferredMethod, profile);
    }

    private async void ChangeDeviceButton_Click(object? sender, EventArgs e)
    {
        if (_controller.State == ConnectionState.Connecting || _controller.State == ConnectionState.Connected)
        {
            await _controller.DisconnectAsync();
        }

        using var picker = new DevicePickerForm();
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedAddress == null) return;

        await ConnectToAsync(picker.SelectedAddress, picker.SelectedName, picker.SelectedMethod, picker.SelectedProfile);
    }

    /// <summary>共用连接流程：从偏好读取游戏模式实现方式并调用 <see cref="PodController.ConnectAsync"/>。</summary>
    private async Task ConnectToAsync(BluetoothAddress address, string name, RfcommConnectionMethod method, DeviceProfile profile)
    {
        var impl = GameModeImplementationExtensions.FromPreference(Preferences.GameModeImplementation);
        _controller.SetGameModeImplementation(impl);

        try
        {
            await _controller.ConnectAsync(address, name, method, impl, profile);
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

    private void BuildAncButtons(DeviceProfile profile)
    {
        _currentProfile = profile;
        _ancMainButtons.Clear();
        _ancLevelButtons.Clear();

        ancButtonPanel.SuspendLayout();
        ancButtonPanel.Controls.Clear();
        _ancSubPanel = null;

        var levels = profile.AncModes.Where(m => IsLevelMode(m.Mode)).ToList();
        var mains = profile.AncModes.Where(m => !IsLevelMode(m.Mode)).ToList();
        bool useSubPanel = levels.Count >= 2;

        var mainRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0)
        };

        foreach (var def in mains)
        {
            mainRow.Controls.Add(MakeAncRadio(def.DisplayName, def.Mode, isLevel: false));
        }

        if (useSubPanel)
        {
            mainRow.Controls.Add(MakeAncRadio("降噪", levels[0].Mode, isLevel: false));
        }
        else
        {
            foreach (var def in levels)
            {
                mainRow.Controls.Add(MakeAncRadio(def.DisplayName, def.Mode, isLevel: false));
            }
        }

        ancButtonPanel.Controls.Add(mainRow);

        if (useSubPanel)
        {
            _ancSubPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(24, 6, 0, 0),
                Visible = false
            };
            foreach (var def in levels)
            {
                _ancSubPanel.Controls.Add(MakeAncRadio(def.DisplayName, def.Mode, isLevel: true));
            }
            ancButtonPanel.Controls.Add(_ancSubPanel);
        }

        ancButtonPanel.ResumeLayout(true);

        UpdateAncUi(_controller.AncMode);
    }

    private RadioButton MakeAncRadio(string text, NoiseControlMode mode, bool isLevel)
    {
        var rb = new RadioButton
        {
            Text = text,
            Tag = mode,
            AutoSize = true,
            Margin = new Padding(4, 2, 12, 2)
        };
        rb.CheckedChanged += AncButton_CheckedChanged;
        if (isLevel) _ancLevelButtons.Add(rb);
        else _ancMainButtons.Add(rb);
        return rb;
    }

    private static bool IsLevelMode(NoiseControlMode mode) =>
        mode == NoiseControlMode.NoiseCancellationSmart ||
        mode == NoiseControlMode.NoiseCancellationLight ||
        mode == NoiseControlMode.NoiseCancellationMedium ||
        mode == NoiseControlMode.NoiseCancellationDeep;

    private async void AncButton_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressAncEvents) return;
        if (sender is not RadioButton rb || !rb.Checked) return;
        if (_controller.State != ConnectionState.Connected) return;

        var mode = (NoiseControlMode)rb.Tag!;

        if (_ancLevelButtons.Contains(rb))
        {
            _suppressAncEvents = true;
            try
            {
                foreach (var mb in _ancMainButtons)
                {
                    if (IsLevelMode((NoiseControlMode)mb.Tag!)) mb.Checked = true;
                    else mb.Checked = false;
                }
            }
            finally { _suppressAncEvents = false; }
        }
        else if (IsLevelMode(mode) && _ancSubPanel != null)
        {
            _ancSubPanel.Visible = true;
            _suppressAncEvents = true;
            try
            {
                if (_ancLevelButtons.Count > 0) _ancLevelButtons[0].Checked = true;
            }
            finally { _suppressAncEvents = false; }
            mode = (NoiseControlMode)_ancLevelButtons[0].Tag!;
        }
        else
        {
            if (_ancSubPanel != null) _ancSubPanel.Visible = false;
        }

        await _controller.SetAncModeAsync(mode);
    }

    private async void EqRawIdInput_ValueChanged(object? sender, EventArgs e)
    {
        if (_suppressEqEvents) return;
        if (_controller.State != ConnectionState.Connected) return;
        await _controller.SetEqPresetAsync((byte)eqRawIdInput.Value);
    }

    private void OnStateChanged(object? sender, ConnectionState state)
    {
        if (InvokeRequired) { BeginInvoke(() => OnStateChanged(sender, state)); return; }
        if (state == ConnectionState.Connected)
        {
            BuildAncButtons(_controller.Profile);
            BuildEqUi(_controller.Profile);
        }
        UpdateConnectionUi();
        UpdateAncUi(_controller.AncMode);
        UpdateEqUi(_controller.EqPresetId);
        RelayoutDynamicGroups();
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

    private void OnEqPresetChanged(object? sender, byte? presetId)
    {
        if (InvokeRequired) { BeginInvoke(() => OnEqPresetChanged(sender, presetId)); return; }
        UpdateEqUi(presetId);
    }

    private void OnCapabilitiesChanged(object? sender, DeviceCapabilities cap)
    {
        if (InvokeRequired) { BeginInvoke(() => OnCapabilitiesChanged(sender, cap)); return; }
        UpdateEqUi(_controller.EqPresetId);
        RelayoutDynamicGroups();
    }

    /// <summary>佩戴状态变化时刷新电量显示（在盒中→闪电图标）。</summary>
    private void OnWearStatusChanged(object? sender, WearStatus wear)
    {
        if (InvokeRequired) { BeginInvoke(() => OnWearStatusChanged(sender, wear)); return; }
        UpdateBatteryUi(_controller.Battery);
    }

    /// <summary>
    /// 按 profile 构建 EQ 控件。优先用 profile 预设名下拉框；
    /// profile 无预设名时回退到原始 preset id 输入框。
    /// </summary>
    private void BuildEqUi(DeviceProfile profile)
    {
        _currentProfile = profile;
        bool hasPresets = profile.EqPresets.Count > 0;

        eqPresetCombo.Visible = hasPresets;
        eqPresetLabel.Visible = hasPresets;
        eqRawIdInput.Visible = !hasPresets;
        eqRawIdLabel.Visible = !hasPresets;

        if (hasPresets)
        {
            _suppressEqEvents = true;
            try
            {
                eqPresetCombo.Items.Clear();
                eqPresetCombo.DisplayMember = nameof(EqPresetDef.DisplayName);
                foreach (var preset in profile.EqPresets)
                {
                    eqPresetCombo.Items.Add(preset);
                }
            }
            finally { _suppressEqEvents = false; }
        }
    }

    /// <summary>根据当前 EQ 预设 id 与运行时能力同步 EQ 控件可见性与选中项。</summary>
    private void UpdateEqUi(byte? presetId)
    {
        eqGroup.Visible = _controller.Capabilities.SupportsEq;
        if (!eqGroup.Visible)
        {
            _suppressEqEvents = true;
            try
            {
                eqPresetCombo.SelectedIndex = -1;
                eqRawIdInput.Value = 0;
            }
            finally { _suppressEqEvents = false; }
            return;
        }

        bool connected = _controller.State == ConnectionState.Connected;
        _suppressEqEvents = true;
        try
        {
            if (eqPresetCombo.Visible)
            {
                eqPresetCombo.SelectedIndex = -1;
                if (presetId.HasValue)
                {
                    for (int i = 0; i < _currentProfile.EqPresets.Count; i++)
                    {
                        if (_currentProfile.EqPresets[i].PresetId == presetId.Value)
                        {
                            eqPresetCombo.SelectedIndex = i;
                            break;
                        }
                    }
                }
                eqPresetCombo.Enabled = connected;
            }
            else
            {
                eqRawIdInput.Value = presetId.HasValue ? Math.Clamp((int)presetId.Value, 0, 255) : 0;
                eqRawIdInput.Enabled = connected;
            }
        }
        finally { _suppressEqEvents = false; }
    }

    private async void EqPresetCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressEqEvents) return;
        if (_controller.State != ConnectionState.Connected) return;
        if (eqPresetCombo.SelectedItem is not EqPresetDef preset) return;
        await _controller.SetEqPresetAsync(preset.PresetId);
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
            _ => (SystemColors.ControlDark, "未连接")
        };
        statusDotLabel.ForeColor = color;
        statusTextLabel.Text = text;
        refreshButton.Enabled = _controller.State == ConnectionState.Connected;
    }

    /// <summary>
    /// 更新电量显示。耳机在充电盒中（WearState.InCase）视为充电，显示闪电图标，
    /// 与充电盒自身充电的闪电样式一致。
    /// </summary>
    private void UpdateBatteryUi(BatteryParams battery)
    {
        var wear = _controller.Capabilities.WearStatus;
        leftBatteryLabel.Text = FormatBattery(battery.Left, "左耳", IsEarbudCharging(wear?.Left, battery.Left?.IsCharging));
        rightBatteryLabel.Text = FormatBattery(battery.Right, "右耳", IsEarbudCharging(wear?.Right, battery.Right?.IsCharging));
        caseBatteryLabel.Text = FormatBattery(battery.Case, "充电盒", battery.Case?.IsCharging ?? false);
    }

    /// <summary>耳机充电判定：在充电盒中，或设备上报 IsCharging。</summary>
    private static bool IsEarbudCharging(WearState? wear, bool? reported) =>
        wear == WearState.InCase || (reported ?? false);

    private static string FormatBattery(PodParams? p, string name, bool charging)
    {
        if (p == null || !p.IsConnected) return $"{name}\n--";
        var charge = charging ? " ⚡" : string.Empty;
        return $"{name}\n{p.Battery}%{charge}";
    }

    private void UpdateAncUi(NoiseControlMode mode)
    {
        _suppressAncEvents = true;
        try
        {
            if (IsLevelMode(mode))
            {
                foreach (var mb in _ancMainButtons)
                {
                    if (IsLevelMode((NoiseControlMode)mb.Tag!)) mb.Checked = true;
                    else mb.Checked = false;
                }
                if (_ancSubPanel != null)
                {
                    _ancSubPanel.Visible = true;
                    foreach (var lb in _ancLevelButtons)
                    {
                        lb.Checked = ((NoiseControlMode)lb.Tag!) == mode;
                    }
                }
            }
            else
            {
                foreach (var mb in _ancMainButtons)
                {
                    mb.Checked = ((NoiseControlMode)mb.Tag!) == mode;
                }
                foreach (var lb in _ancLevelButtons)
                {
                    lb.Checked = false;
                }
                if (_ancSubPanel != null) _ancSubPanel.Visible = false;
            }
        }
        finally
        {
            _suppressAncEvents = false;
        }

        bool enabled = _controller.State == ConnectionState.Connected;
        foreach (var mb in _ancMainButtons) mb.Enabled = enabled;
        foreach (var lb in _ancLevelButtons) lb.Enabled = enabled;
    }

    /// <summary>
    /// 动态重排 EQ 与日志分组：EQ 不支持时隐藏并不留白，日志上移补位。
    /// 锚点为 ancGroup 底部，间距 12px。
    /// </summary>
    private void RelayoutDynamicGroups()
    {
        int y = ancGroup.Bottom + 12;
        if (eqGroup.Visible)
        {
            if (eqGroup.Top != y) eqGroup.Top = y;
            y = eqGroup.Bottom + 12;
        }
        if (logGroup.Top != y) logGroup.Top = y;
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
