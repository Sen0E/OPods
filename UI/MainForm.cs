using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using OPods.Controllers;
using OPods.Pods;

namespace OPods.UI;

/// <summary>
/// Main application window: shows device/battery/ANC/game-mode state and
/// wires user actions to <see cref="PodController"/>。协议优先重构后，EQ/游戏模式/
/// 空间音频等能力的可见性由运行时 <see cref="PodController.Capabilities"/> 决定，
/// 设备信息（编解码器、佩戴状态）由主动通知实时更新。
/// </summary>
public partial class MainForm : Form
{
    private readonly PodController _controller = new();
    private bool _suppressAncEvents;
    private bool _suppressGameModeEvents;
    private bool _suppressEqEvents;
    private bool _suppressSpatialEvents;
    private bool _suppressMultiDeviceEvents;

    private readonly List<RadioButton> _ancMainButtons = new();
    private readonly List<RadioButton> _ancLevelButtons = new();
    private FlowLayoutPanel? _ancSubPanel;
    private DeviceProfile _currentProfile = DeviceProfileRegistry.Default;

    public MainForm()
    {
        InitializeComponent();
        gameModeImplCombo.SelectedIndex = GameModeImplementationExtensions.SelectedIndexOf(
            GameModeImplementationExtensions.FromPreference(Preferences.GameModeImplementation));

        _controller.StateChanged += OnStateChanged;
        _controller.BatteryChanged += OnBatteryChanged;
        _controller.AncModeChanged += OnAncModeChanged;
        _controller.GameModeChanged += OnGameModeChanged;
        _controller.EqPresetChanged += OnEqPresetChanged;
        _controller.CapabilitiesChanged += OnCapabilitiesChanged;
        _controller.WearStatusChanged += OnWearStatusChanged;
        _controller.SpatialAudioChanged += OnSpatialAudioChanged;
        _controller.Log += OnLog;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        BuildAncButtons(DeviceProfileRegistry.Default);
        BuildEqUi(DeviceProfileRegistry.Default);
        UpdateConnectionUi();
        UpdateBatteryUi(_controller.Battery);
        UpdateAncUi(_controller.AncMode);
        UpdateGameModeUi(_controller.GameMode);
        UpdateEqUi(_controller.EqPresetId);
        UpdateCapabilitiesUi();
        UpdateDeviceInfoUi();
        AppendLog("OPods 已启动。正在从系统配对表自动检测 OPPO 耳机…");

        // 启动时自动检测已连接的 OPPO 耳机并连接；未检测到时提示用户手动选择。
        _ = AutoDetectAndConnectAsync();
    }

    /// <summary>
    /// 启动时自动检测已连接的 OPPO 耳机。直接读取系统已配对设备列表
    /// （<see cref="BluetoothClient.PairedDevices"/>，不触发无线电扫描，毫秒级返回），
    /// 筛选设备名以 "OPPO Enco" 开头的，优先选择已连接（Connected）的设备。
    /// 匹配到后按设备名解析 DeviceProfile —— 未命中任何已注册机型时
    /// <see cref="DeviceProfileRegistry.Resolve"/> 会自动返回
    /// <see cref="GenericOppoProfile"/> 兜底配置。
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
                // 使用 PairedDevices 而非 DiscoverDevices()，跳过 ~10 秒的无线电扫描，
                // 直接从 Windows 系统配对表读取（fIssueInquiry=false, fReturnAuthenticated=true）。
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

            // 优先选已连接音频的，其次选已配对的，最后取第一个候选
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

        // 按蓝牙名解析机型 profile；未匹配到已注册机型时自动降级为 GenericOppoProfile
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

    /// <summary>共用连接流程：设置游戏模式实现方式并调用 <see cref="PodController.ConnectAsync"/>。</summary>
    private async Task ConnectToAsync(BluetoothAddress address, string name, RfcommConnectionMethod method, DeviceProfile profile)
    {
        var impl = GameModeImplementationExtensions.FromSelectedIndex(gameModeImplCombo.SelectedIndex);
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
            // 主行加一个"降噪"按钮，Tag=第一个降噪分级（通常是 Smart）
            mainRow.Controls.Add(MakeAncRadio("降噪", levels[0].Mode, isLevel: false));
        }
        else
        {
            // 单个降噪分级：直接作为主按钮显示
            foreach (var def in levels)
            {
                mainRow.Controls.Add(MakeAncRadio(def.DisplayName, def.Mode, isLevel: false));
            }
        }

        // 主行在上
        ancButtonPanel.Controls.Add(mainRow);

        // 子行（降噪分级）在下，初始隐藏
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
            // 点击降噪分级子按钮：保持"降噪"主按钮选中
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
            // 点击"降噪"主按钮：显示子行，默认选中第一个分级
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
            // 点击其他主按钮：隐藏子行
            if (_ancSubPanel != null) _ancSubPanel.Visible = false;
        }

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

    private async void SpatialAudioCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressSpatialEvents) return;
        if (_controller.State != ConnectionState.Connected) return;
        await _controller.SetSpatialAudioAsync(spatialAudioCheckBox.Checked);
    }

    private async void MultiDeviceCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressMultiDeviceEvents) return;
        if (_controller.State != ConnectionState.Connected) return;
        await _controller.SetMultiDeviceAsync(multiDeviceCheckBox.Checked);
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
        UpdateCapabilitiesUi();
        UpdateDeviceInfoUi();
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

    private void OnEqPresetChanged(object? sender, byte? presetId)
    {
        if (InvokeRequired) { BeginInvoke(() => OnEqPresetChanged(sender, presetId)); return; }
        UpdateEqUi(presetId);
    }

    private void OnCapabilitiesChanged(object? sender, DeviceCapabilities cap)
    {
        if (InvokeRequired) { BeginInvoke(() => OnCapabilitiesChanged(sender, cap)); return; }
        UpdateCapabilitiesUi();
        UpdateDeviceInfoUi();
        // 能力变化可能影响 EQ 控件的可用性（SupportsEq）
        UpdateEqUi(_controller.EqPresetId);
    }

    private void OnWearStatusChanged(object? sender, WearStatus wear)
    {
        if (InvokeRequired) { BeginInvoke(() => OnWearStatusChanged(sender, wear)); return; }
        UpdateWearStatusUi(wear);
    }

    private void OnSpatialAudioChanged(object? sender, bool enabled)
    {
        if (InvokeRequired) { BeginInvoke(() => OnSpatialAudioChanged(sender, enabled)); return; }
        UpdateSpatialAudioUi(enabled);
    }

    /// <summary>
    /// 按 profile 与运行时能力构建 EQ 控件。
    /// 优先使用 profile 提供的预设名下拉框；profile 无预设名时回退到原始 preset id 输入框；
    /// 运行时未确认支持 EQ 时隐藏整个 EQ 分组。
    /// </summary>
    private void BuildEqUi(DeviceProfile profile)
    {
        _currentProfile = profile;
        bool hasPresets = profile.EqPresets.Count > 0;

        // 预设名下拉框 vs 原始 id 输入框：二选一显示
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

    /// <summary>根据当前 EQ 预设 id 同步下拉框 / 输入框选中项。</summary>
    private void UpdateEqUi(byte? presetId)
    {
        // 整个 EQ 分组可见性由运行时能力决定
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
            // 语义色 Green/Orange/Red 在浅色与深色背景下均清晰可辨；
            // 未连接用 SystemColors.ControlDark，随主题自动映射。
            ConnectionState.Connected => (Color.Green, "已连接"),
            ConnectionState.Connecting => (Color.Orange, "连接中…"),
            ConnectionState.Error => (Color.Red, "连接错误"),
            _ => (SystemColors.ControlDark, "未连接")
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
            if (IsLevelMode(mode))
            {
                // 降噪分级：选中"降噪"主按钮 + 对应分级子按钮，显示子行
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
                // Off/Adaptive/Transparency：选中对应主按钮，隐藏子行
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

    /// <summary>按运行时能力调整 EQ / 游戏模式 / 空间音频分组的可见性。</summary>
    private void UpdateCapabilitiesUi()
    {
        var cap = _controller.Capabilities;
        bool connected = _controller.State == ConnectionState.Connected;

        // 空间音频：能力发现确认支持才显示
        spatialAudioGroup.Visible = connected && cap.SupportsSpatialAudio;
        if (spatialAudioGroup.Visible)
        {
            UpdateSpatialAudioUi(cap.SpatialAudioEnabled);
        }

        // 游戏模式：能力发现确认支持才显示
        gameModeGroup.Visible = connected && cap.SupportsGameMode;
        if (!gameModeGroup.Visible)
        {
            _suppressGameModeEvents = true;
            try { gameModeCheckBox.Checked = false; }
            finally { _suppressGameModeEvents = false; }
        }

        // 双设备连接：能力发现确认支持才显示 checkbox
        multiDeviceCheckBox.Visible = connected && cap.SupportsMultiDevice;
        if (multiDeviceCheckBox.Visible)
        {
            UpdateMultiDeviceUi(cap.IsFeatureEnabled(FeatureId.MULTI_DEVICES_CONNECT));
        }

        // EQ 可见性由 UpdateEqUi 处理（依赖 SupportsEq）
        UpdateEqUi(_controller.EqPresetId);

        // 动态重排：隐藏的分组不留白，后续分组自动上移补位
        RelayoutDynamicGroups();
    }

    /// <summary>
    /// 按可见性重排动态分组（游戏模式/空间音频/EQ），隐藏的分组不留白。
    /// 锚点为 ancGroup 底部，各分组间距 12px。
    /// </summary>
    private void RelayoutDynamicGroups()
    {
        int y = ancGroup.Bottom + 12;
        foreach (var g in new[] { gameModeGroup, spatialAudioGroup, eqGroup })
        {
            if (!g.Visible) continue;
            if (g.Top != y) g.Top = y;
            y = g.Bottom + 12;
        }

        // 日志分组紧跟最后一个可见分组
        logGroup.Top = y;
    }

    /// <summary>更新设备信息分组：编解码器 + 双设备连接状态。</summary>
    private void UpdateDeviceInfoUi()
    {
        codecValueLabel.Text = string.IsNullOrEmpty(_controller.CodecName) ? "--" : _controller.CodecName;
        UpdateWearStatusUi(_controller.WearStatus);
        UpdateMultiDeviceUi(_controller.Capabilities.IsFeatureEnabled(FeatureId.MULTI_DEVICES_CONNECT));
    }

    private void UpdateWearStatusUi(WearStatus? wear)
    {
        wearLeftLabel.Text = wear == null ? "左耳：--" : $"左耳：{WearStateText(wear.Left)}";
        wearRightLabel.Text = wear == null ? "右耳：--" : $"右耳：{WearStateText(wear.Right)}";
    }

    private static string WearStateText(WearState state) => state switch
    {
        WearState.Wearing => "已佩戴",
        WearState.InCase => "在充电盒",
        WearState.Removed => "已取出未戴",
        WearState.Disconnected => "未连接",
        _ => "未知"
    };

    private void UpdateSpatialAudioUi(bool enabled)
    {
        _suppressSpatialEvents = true;
        try
        {
            spatialAudioCheckBox.Checked = enabled;
        }
        finally
        {
            _suppressSpatialEvents = false;
        }
        spatialAudioCheckBox.Enabled = _controller.State == ConnectionState.Connected;
    }

    private void UpdateMultiDeviceUi(bool enabled)
    {
        _suppressMultiDeviceEvents = true;
        try
        {
            multiDeviceCheckBox.Checked = enabled;
        }
        finally
        {
            _suppressMultiDeviceEvents = false;
        }
        multiDeviceCheckBox.Enabled = _controller.State == ConnectionState.Connected;
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
