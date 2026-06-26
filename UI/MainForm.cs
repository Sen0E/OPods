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

    private readonly List<AncOptionBox> _ancMainButtons = new();
    private readonly List<AncOptionBox> _ancLevelButtons = new();
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
        // 窗口定位于屏幕右下角，留 16px 边距
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromControl(this).WorkingArea;
        Location = new Point(workArea.Right - Width - 16, workArea.Bottom - Height - 16);

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

        // 主选项行：圆角矩形卡片，等宽分布
        var mainRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0)
        };

        int mainCount = mains.Count + (useSubPanel ? 1 : levels.Count);
        int mainBoxWidth = ComputeBoxWidth(ancButtonPanel.Width - 20, mainCount, 12);

        foreach (var def in mains)
        {
            mainRow.Controls.Add(MakeAncOption(def.DisplayName, def.Mode, isLevel: false, mainBoxWidth, 48));
        }

        if (useSubPanel)
        {
            mainRow.Controls.Add(MakeAncOption("降噪", levels[0].Mode, isLevel: false, mainBoxWidth, 48));
        }
        else
        {
            foreach (var def in levels)
            {
                mainRow.Controls.Add(MakeAncOption(def.DisplayName, def.Mode, isLevel: false, mainBoxWidth, 48));
            }
        }

        ancButtonPanel.Controls.Add(mainRow);

        if (useSubPanel)
        {
            // 子选项行：降噪模式下展开的 4 个等级卡片，缩进对齐主选项区
            _ancSubPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 10, 0, 0),
                Visible = false
            };
            int subBoxWidth = ComputeBoxWidth(ancButtonPanel.Width - 20, levels.Count, 10);
            foreach (var def in levels)
            {
                _ancSubPanel.Controls.Add(MakeAncOption(def.DisplayName, def.Mode, isLevel: true, subBoxWidth, 44));
            }
            ancButtonPanel.Controls.Add(_ancSubPanel);
        }

        ancButtonPanel.ResumeLayout(true);

        UpdateAncUi(_controller.AncMode);
    }

    private static int ComputeBoxWidth(int availableWidth, int count, int gap)
    {
        if (count <= 0) return 120;
        int total = availableWidth - gap * (count - 1);
        return Math.Max(96, total / count);
    }

    private AncOptionBox MakeAncOption(string text, NoiseControlMode mode, bool isLevel, int width, int height)
    {
        var box = new AncOptionBox(text)
        {
            Tag = mode,
            Size = new Size(width, height),
            Margin = new Padding(0, 0, isLevel ? 10 : 12, 0)
        };
        box.OptionSelected += AncOption_Selected;
        if (isLevel) _ancLevelButtons.Add(box);
        else _ancMainButtons.Add(box);
        return box;
    }

    private static bool IsLevelMode(NoiseControlMode mode) =>
        mode == NoiseControlMode.NoiseCancellationSmart ||
        mode == NoiseControlMode.NoiseCancellationLight ||
        mode == NoiseControlMode.NoiseCancellationMedium ||
        mode == NoiseControlMode.NoiseCancellationDeep;

    private async void AncOption_Selected(object? sender, EventArgs e)
    {
        if (_suppressAncEvents) return;
        if (sender is not AncOptionBox box) return;
        if (_controller.State != ConnectionState.Connected) return;

        var mode = (NoiseControlMode)box.Tag!;

        if (_ancLevelButtons.Contains(box))
        {
            // 点击子选项：保持主选项「降噪」高亮，仅切换子选项高亮
            _suppressAncEvents = true;
            try
            {
                foreach (var lb in _ancLevelButtons) lb.Selected = lb == box;
                foreach (var mb in _ancMainButtons)
                {
                    if (IsLevelMode((NoiseControlMode)mb.Tag!)) mb.Selected = true;
                    else mb.Selected = false;
                }
            }
            finally { _suppressAncEvents = false; }
        }
        else if (IsLevelMode(mode) && _ancSubPanel != null)
        {
            // 点击主选项「降噪」：展开子选项行并默认选中第一档
            _ancSubPanel.Visible = true;
            _suppressAncEvents = true;
            try
            {
                foreach (var mb in _ancMainButtons) mb.Selected = IsLevelMode((NoiseControlMode)mb.Tag!);
                if (_ancLevelButtons.Count > 0) _ancLevelButtons[0].Selected = true;
            }
            finally { _suppressAncEvents = false; }
            mode = (NoiseControlMode)_ancLevelButtons[0].Tag!;
        }
        else
        {
            // 点击其它主选项：折叠子选项行，仅当前主选项高亮
            if (_ancSubPanel != null) _ancSubPanel.Visible = false;
            _suppressAncEvents = true;
            try
            {
                foreach (var mb in _ancMainButtons) mb.Selected = mb == box;
                foreach (var lb in _ancLevelButtons) lb.Selected = false;
            }
            finally { _suppressAncEvents = false; }
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
                    mb.Selected = IsLevelMode((NoiseControlMode)mb.Tag!);
                }
                if (_ancSubPanel != null)
                {
                    _ancSubPanel.Visible = true;
                    foreach (var lb in _ancLevelButtons)
                    {
                        lb.Selected = ((NoiseControlMode)lb.Tag!) == mode;
                    }
                }
            }
            else
            {
                foreach (var mb in _ancMainButtons)
                {
                    mb.Selected = ((NoiseControlMode)mb.Tag!) == mode;
                }
                foreach (var lb in _ancLevelButtons)
                {
                    lb.Selected = false;
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

/// <summary>
/// 降噪模式圆角矩形选项卡片：以卡片形式展示模式名称，
/// 选中时绘制彩色高亮边框，未选中时为浅灰描边，鼠标悬停时背景微亮。
/// 替代原有 RadioButton 风格，提升「关闭 / 降噪 / 通透」主选项与
/// 「智能 / 轻度 / 中度 / 深度」子选项的视觉一致性。
/// </summary>
internal sealed class AncOptionBox : Panel
{
    private const int CornerRadius = 14;
    private static readonly Color SelectedColor = Color.FromArgb(0, 120, 215);
    private static readonly Color SelectedBg = Color.FromArgb(235, 244, 255);
    private static readonly Color UnselectedColor = Color.FromArgb(210, 214, 220);
    private static readonly Color HoverBg = Color.FromArgb(248, 249, 251);
    private static readonly Color TextColor = Color.FromArgb(48, 56, 65);

    private bool _selected;
    private bool _hover;
    private readonly string _displayText;

    /// <summary>用户点击该卡片时触发（与 RadioButton.CheckedChanged 等价的语义入口）。</summary>
    public event EventHandler? OptionSelected;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            Invalidate();
        }
    }

    public AncOptionBox(string text)
    {
        _displayText = text;
        Size = new Size(140, 48);
        BackColor = Color.White;
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        OptionSelected?.Invoke(this, e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        if (!_hover)
        {
            _hover = true;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover)
        {
            _hover = false;
            Invalidate();
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = BuildRoundedRect(rect, CornerRadius);

        // 背景：选中 > 悬停 > 默认
        Color bg = _selected ? SelectedBg : (_hover && Enabled ? HoverBg : Color.White);
        using (var bgBrush = new SolidBrush(bg))
        {
            g.FillPath(bgBrush, path);
        }

        // 边框：选中加粗高亮色，否则细线浅灰；禁用时虚化
        Color border = _selected ? SelectedColor : UnselectedColor;
        if (!Enabled) border = Color.FromArgb(230, 232, 236);
        float borderWidth = _selected ? 2.5f : 1f;
        using (var borderPen = new Pen(border, borderWidth))
        {
            g.DrawPath(borderPen, path);
        }

        // 文字：选中着色高亮，禁用灰化
        Color fg = !Enabled
            ? Color.FromArgb(170, 174, 180)
            : (_selected ? SelectedColor : TextColor);
        using var font = new Font("Segoe UI", 10F, FontStyle.Bold);
        using var fgBrush = new SolidBrush(fg);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString(_displayText, font, fgBrush, rect, sf);
    }

    private static System.Drawing.Drawing2D.GraphicsPath BuildRoundedRect(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
