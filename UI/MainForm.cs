using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
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
        _controller.Log += OnLog;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        BuildAncButtons(DeviceProfileRegistry.Default);
        UpdateConnectionUi();
        UpdateBatteryUi(_controller.Battery);
        UpdateAncUi(_controller.AncMode);
        UpdateGameModeUi(_controller.GameMode);
        AppendLog("OPods 已启动。正在自动检测已连接的 OPPO 耳机…");

        // 启动时自动检测已连接的 OPPO 耳机并连接；未检测到时提示用户手动选择。
        _ = AutoDetectAndConnectAsync();
    }

    /// <summary>
    /// 启动时自动检测已连接的 OPPO 耳机。扫描附近蓝牙设备，筛选设备名以
    /// "OPPO Enco" 开头的，优先选择已连接（Connected）的设备，其次选已配对
    /// （Authenticated）的。匹配到后按设备名解析 DeviceProfile —— 未命中任何
    /// 已注册机型时 <see cref="DeviceProfileRegistry.Resolve"/> 会自动返回
    /// <see cref="GenericOppoProfile"/> 兜底配置。
    /// </summary>
    private async Task AutoDetectAndConnectAsync()
    {
        BluetoothDeviceInfo? target = null;
        try
        {
            AppendLog("正在扫描附近蓝牙设备…");
            BluetoothDeviceInfo[] devices;
            using (var client = new BluetoothClient())
            {
                devices = await Task.Run(() => client.DiscoverDevices().ToArray()).ConfigureAwait(true);
            }

            var candidates = devices
                .Where(d => !string.IsNullOrEmpty(d.DeviceName) &&
                            d.DeviceName.StartsWith("OPPO Enco", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 0)
            {
                AppendLog("未检测到 OPPO Enco 系列耳机。请确认耳机已开机并连接，或点击「更换设备」手动选择。");
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

    private void OnStateChanged(object? sender, ConnectionState state)
    {
        if (InvokeRequired) { BeginInvoke(() => OnStateChanged(sender, state)); return; }
        if (state == ConnectionState.Connected)
        {
            BuildAncButtons(_controller.Profile);
        }
        UpdateConnectionUi();
        UpdateAncUi(_controller.AncMode);
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
