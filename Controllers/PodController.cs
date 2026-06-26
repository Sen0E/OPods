using System.Diagnostics;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using OPods.Bluetooth;
using OPods.Pods;

namespace OPods.Controllers;

/// <summary>
/// Standalone RFCOMM controller for OPPO earphones. Owns the connection state
/// machine, background packet reader, 30s battery polling, and command dispatch.
/// 协议优先重构后，连接后先做能力发现（0x0100/0x0105/0x0114/0x012A/0x010D/0x0200），
/// 再注册主动通知（0x0201/0x0205），轮询仅作兜底。
/// </summary>
public sealed class PodController : IDisposable
{
    private const int BatteryPollIntervalMs = 30_000;
    private const string Tag = "OppoPods-PodController";

    private readonly OppoRfcommClient _client = new();
    private CancellationTokenSource _cts = new();
    private Task? _readTask;
    private Task? _pollTask;

    private GameModeImplementation _gameModeImplementation = GameModeImplementation.Standard;
    private bool _running;
    private bool _capabilitiesDiscovered;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public BatteryParams Battery { get; private set; } = BatteryParams.Empty;
    public NoiseControlMode AncMode { get; private set; } = NoiseControlMode.Off;
    public bool GameMode { get; private set; }
    public string DeviceName { get; private set; } = string.Empty;

    /// <summary>
    /// 当前 EQ 预设 id（来自耳机上报 / 查询响应）。null 表示未知或不支持。
    /// </summary>
    public byte? EqPresetId { get; private set; }

    /// <summary>运行时设备能力（协议发现）。未连接或发现未完成时字段为默认值。</summary>
    public DeviceCapabilities Capabilities { get; private set; } = new();

    /// <summary>当前佩戴状态；未上报为 null。</summary>
    public WearStatus? WearStatus => Capabilities.WearStatus;

    /// <summary>空间音频当前开关；不支持为 false。</summary>
    public bool SpatialAudioEnabled => Capabilities.SpatialAudioEnabled;

    /// <summary>当前编解码器友好名；未知为 null。</summary>
    public string? CodecName => Capabilities.CodecName;

    /// <summary>当前连接机型的配置，未连接时为 <see cref="DeviceProfileRegistry.Default"/>。</summary>
    public DeviceProfile Profile { get; private set; } = DeviceProfileRegistry.Default;

    public event EventHandler<ConnectionState>? StateChanged;
    public event EventHandler<BatteryParams>? BatteryChanged;
    public event EventHandler<NoiseControlMode>? AncModeChanged;
    public event EventHandler<bool>? GameModeChanged;
    public event EventHandler<byte?>? EqPresetChanged;
    public event EventHandler<DeviceCapabilities>? CapabilitiesChanged;
    public event EventHandler<WearStatus>? WearStatusChanged;
    public event EventHandler<bool>? SpatialAudioChanged;
    public event EventHandler<string>? Log;

    public async Task ConnectAsync(
        BluetoothAddress address,
        string deviceName,
        RfcommConnectionMethod method,
        GameModeImplementation implementation,
        DeviceProfile profile,
        CancellationToken ct = default)
    {
        if (State == ConnectionState.Connecting) return;

        CancelExisting();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        _gameModeImplementation = implementation;
        Profile = profile;
        DeviceName = deviceName;
        Capabilities = new DeviceCapabilities();
        _capabilitiesDiscovered = false;
        SetState(ConnectionState.Connecting);

        try
        {
            await Task.Delay(300, token).ConfigureAwait(false);
            await _client.ConnectAsync(address, method, profile, token).ConfigureAwait(false);
            LogMsg($"RFCOMM connected to {deviceName}");
            _running = true;
            SetState(ConnectionState.Connected);

            _readTask = Task.Run(() => ReadLoopAsync(token), token);

            await Task.Delay(300, token).ConfigureAwait(false);
            // 首次：能力发现 + 状态查询；后续轮询由 PollLoopAsync 触发
            await QueryStatusAsync(token).ConfigureAwait(false);

            _pollTask = Task.Run(() => PollLoopAsync(token), token);
        }
        catch (OperationCanceledException)
        {
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
        catch (IOException e)
        {
            LogMsg($"RFCOMM connect failed: {e.Message}");
            SetState(ConnectionState.Error);
            _running = false;
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var framer = new OppoPacketFramer();
        try
        {
            await foreach (var frame in _client.ReadFramesAsync(ct).ConfigureAwait(false))
            {
                HandlePacket(frame);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            LogMsg($"Read error: {e.Message}");
        }

        if (_running)
        {
            _running = false;
            await DisconnectAsync().ConfigureAwait(false);
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        try
        {
            while (_running && !ct.IsCancellationRequested)
            {
                await Task.Delay(BatteryPollIntervalMs, ct).ConfigureAwait(false);
                if (_running && !ct.IsCancellationRequested)
                {
                    await QueryStatusAsync(ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void HandlePacket(byte[] packet)
    {
        LogMsg($"Recv: {ToHex(packet)}");

        // 统一入口：0x0204 主动事件先经 NotificationEventParser 分流
        if (OppoPackets.TryGetPacketLayout(packet, out var layout)
            && layout.Cmd == Cmd.NOTIF_EVENT)
        {
            HandleNotificationEvent(packet);
            return;
        }

        var result = BatteryParser.Parse(packet);
        if (result != null)
        {
            Battery = new BatteryParams
            {
                Left = ToPodParams(result.Left),
                Right = ToPodParams(result.Right),
                Case = ToPodParams(result.Case)
            };
            BatteryChanged?.Invoke(this, Battery);
            return;
        }

        var ancResult = AncModeParser.Parse(packet, Profile);
        if (ancResult != null)
        {
            LogMsg($"ANC mode received: {ancResult}");
            AncMode = ancResult.Value;
            AncModeChanged?.Invoke(this, AncMode);
            return;
        }

        var gameModeResult = GameModeParser.Parse(packet, _gameModeImplementation);
        if (gameModeResult != null)
        {
            LogMsg($"Game mode received: {gameModeResult}");
            GameMode = gameModeResult.Value;
            GameModeChanged?.Invoke(this, GameMode);
            return;
        }

        var setFeatureResult = SwitchFeatureSetParser.Parse(packet);
        if (setFeatureResult != null)
        {
            LogMsg($"Switch feature response: status={setFeatureResult.Status}, value={setFeatureResult.Value}");
            // 0x0403 设置成功后，刷新功能开关状态以同步空间音频/游戏模式等
            if (setFeatureResult.Status == 0x00)
            {
                _ = RefreshFeatureSwitchesAsync();
            }
            return;
        }

        var eqResult = EqPresetParser.Parse(packet);
        if (eqResult != null)
        {
            LogMsg($"EQ preset received: id={eqResult}");
            EqPresetId = eqResult;
            // 收到 EQ 响应即视为支持 EQ
            if (!Capabilities.SupportsEq)
            {
                Capabilities.SupportsEq = true;
                CapabilitiesChanged?.Invoke(this, Capabilities);
            }
            EqPresetChanged?.Invoke(this, EqPresetId);
            return;
        }

        // 能力发现响应
        HandleCapabilityResponse(packet);
    }

    /// <summary>处理 0x0204 主动事件，按 eventCode 派发到子解析器。</summary>
    private void HandleNotificationEvent(byte[] packet)
    {
        var ev = NotificationEventParser.Parse(packet);
        if (ev == null)
        {
            LogMsg("Notification event: parse failed");
            return;
        }

        switch (ev.EventCode)
        {
            case NotificationEventCode.BATTERY:
                if (ev.Battery != null)
                {
                    var current = Battery;
                    Battery = new BatteryParams
                    {
                        Left = MergePodParams(current.Left, ev.Battery.Left),
                        Right = MergePodParams(current.Right, ev.Battery.Right),
                        Case = MergePodParams(current.Case, ev.Battery.Case)
                    };
                    BatteryChanged?.Invoke(this, Battery);
                }
                break;

            case NotificationEventCode.WEAR_STATUS:
                if (ev.Wear != null)
                {
                    Capabilities.WearStatus = ev.Wear;
                    LogMsg($"Wear status: L={ev.Wear.Left}, R={ev.Wear.Right}");
                    WearStatusChanged?.Invoke(this, ev.Wear);
                }
                break;

            case NotificationEventCode.ANC:
                {
                    var anc = AncModeParser.Parse(packet, Profile);
                    if (anc != null)
                    {
                        LogMsg($"ANC mode received: {anc}");
                        AncMode = anc.Value;
                        AncModeChanged?.Invoke(this, AncMode);
                    }
                    break;
                }

            default:
                LogMsg($"Notification event {ev.EventName} (len={ev.RawPayload?.Length ?? 0})");
                break;
        }
    }

    /// <summary>处理能力发现相关响应（0x8100/0x8114/0x812A/0x810D/0x8200/0x8201/0x8205）。</summary>
    private void HandleCapabilityResponse(byte[] packet)
    {
        if (!OppoPackets.TryGetPacketLayout(packet, out var layout)) return;
        bool changed = false;

        switch (layout.Cmd)
        {
            case Cmd.CAPABILITY_RESPONSE:
                {
                    var cap = CapabilityParser.Parse(packet);
                    if (cap != null) { Capabilities.RemoteCapability = cap; changed = true; }
                    break;
                }
            case Cmd.CODEC_RESPONSE:
                {
                    var codec = CodecParser.Parse(packet);
                    if (codec != null) { Capabilities.CodecTypeCode = codec; changed = true; }
                    break;
                }
            case Cmd.SPATIAL_TYPE_RESPONSE:
                {
                    var st = SpatialTypeParser.Parse(packet);
                    if (st != null) { Capabilities.SpatialType = st; changed = true; }
                    break;
                }
            case Cmd.QUERY_STATUS_RESPONSE:
                {
                    var fs = FeatureSwitchParser.Parse(packet);
                    if (fs != null)
                    {
                        bool prevSpatial = Capabilities.SpatialAudioEnabled;
                        Capabilities.FeatureSwitches = fs;
                        // 同步游戏模式状态
                        var gm = fs.ToGameModeStatus().EnabledFor(_gameModeImplementation);
                        if (gm != null && gm != GameMode)
                        {
                            GameMode = gm.Value;
                            GameModeChanged?.Invoke(this, GameMode);
                        }
                        // 同步空间音频开关
                        if (Capabilities.SpatialAudioEnabled != prevSpatial)
                        {
                            SpatialAudioChanged?.Invoke(this, Capabilities.SpatialAudioEnabled);
                        }
                        changed = true;
                    }
                    break;
                }
            case Cmd.NOTIF_CAPABILITY_RESPONSE:
                {
                    var nc = NotificationCapabilityParser.Parse(packet);
                    if (nc != null)
                    {
                        Capabilities.NotificationCapability = nc;
                        changed = true;
                        // 能力响应到达后立即注册通知
                        _ = RegisterNotificationsAsync(nc);
                    }
                    break;
                }
            case Cmd.NOTIF_REGISTER_RESPONSE:
            case Cmd.NOTIF_REGISTER_MULTI_RESPONSE:
                {
                    var rr = NotificationRegisterParser.Parse(packet);
                    LogMsg($"Notif register response: status={rr?.Status}, count={rr?.RegisteredCount}");
                    break;
                }
        }

        if (changed)
        {
            CapabilitiesChanged?.Invoke(this, Capabilities);
        }
    }

    private static PodParams? ToPodParams(BatteryInfo? info)
    {
        if (info == null) return null;
        return new PodParams
        {
            Battery = info.Level,
            IsCharging = info.IsCharging,
            IsConnected = true,
            RawStatus = 0
        };
    }

    private static PodParams? MergePodParams(PodParams? current, BatteryInfo? updated)
    {
        if (updated == null) return current;
        return new PodParams
        {
            Battery = updated.Level,
            IsCharging = updated.IsCharging,
            IsConnected = true,
            RawStatus = current?.RawStatus ?? 0
        };
    }

    /// <summary>
    /// 状态查询。首次调用会先执行能力发现（0x0100/0x0114/0x012A/0x010D/0x0200），
    /// 之后轮询电量/ANC/EQ/功能开关。保留 30s 轮询作为通知未到达时的兜底。
    /// </summary>
    private async Task QueryStatusAsync(CancellationToken ct)
    {
        try
        {
            if (!_capabilitiesDiscovered)
            {
                await DiscoverCapabilitiesAsync(ct).ConfigureAwait(false);
                _capabilitiesDiscovered = true;
            }

            await SendAsync(OppoEnums.QueryFeatureSwitchAll, ct).ConfigureAwait(false);
            await Task.Delay(50, ct).ConfigureAwait(false);
            await SendAsync(OppoEnums.QueryBattery, ct).ConfigureAwait(false);
            await Task.Delay(50, ct).ConfigureAwait(false);
            await SendAsync(OppoEnums.QueryAnc, ct).ConfigureAwait(false);
            if (Capabilities.SupportsEq)
            {
                await Task.Delay(50, ct).ConfigureAwait(false);
                await SendAsync(OppoEnums.QueryEq, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            LogMsg($"QueryStatus failed: {e.Message}");
        }
    }

    /// <summary>能力发现流程：依次发送 0x0100/0x0114/0x012A/0x010D/0x010F/0x0200。单项失败不阻断整体。</summary>
    private async Task DiscoverCapabilitiesAsync(CancellationToken ct)
    {
        // EQ 查询放入发现阶段：设备若支持会回 0x810F 并置位 SupportsEq，
        // 避免「仅当 SupportsEq 才查询 EQ」的鸡蛋问题导致 EQ 分组永远隐藏。
        var queries = new (string name, byte[] packet)[]
        {
            ("capability", OppoEnums.QueryCapability),
            ("codec", OppoEnums.QueryCodec),
            ("spatialType", OppoEnums.QuerySpatialType),
            ("featureSwitch", OppoEnums.QueryFeatureSwitchAll),
            ("eq", OppoEnums.QueryEq),
            ("notifCapability", OppoEnums.QueryNotifCapability),
        };

        foreach (var (name, packet) in queries)
        {
            try
            {
                await SendAsync(packet, ct).ConfigureAwait(false);
                await Task.Delay(80, ct).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                LogMsg($"Discover {name} failed: {e.Message}");
            }
        }
    }

    /// <summary>按 0x8200 能力响应注册主动通知：支持 0x0205 用批量，否则逐个 0x0201。</summary>
    private async Task RegisterNotificationsAsync(NotificationCapability nc)
    {
        try
        {
            var codes = nc.SupportedEventCodes
                .Where(c => c != (Cmd.NOTIF_REGISTER_MULTI & 0xFF))
                .ToArray();
            if (codes.Length == 0) return;

            if (nc.SupportsMultiRegister)
            {
                await SendAsync(OppoEnums.BuildNotifRegisterMulti(codes), _cts.Token).ConfigureAwait(false);
                LogMsg($"Registered {codes.Length} notifications via 0x0205: {string.Join(",", codes.Select(c => $"0x{c:X2}"))}");
            }
            else
            {
                foreach (var c in codes)
                {
                    await SendAsync(OppoEnums.BuildNotifRegister(c), _cts.Token).ConfigureAwait(false);
                    await Task.Delay(30, _cts.Token).ConfigureAwait(false);
                }
                LogMsg($"Registered {codes.Length} notifications via 0x0201");
            }
        }
        catch (Exception e)
        {
            LogMsg($"Register notifications failed: {e.Message}");
        }
    }

    /// <summary>手动刷新功能开关状态（0x010D）。</summary>
    private async Task RefreshFeatureSwitchesAsync()
    {
        try
        {
            await SendAsync(OppoEnums.QueryFeatureSwitchAll, _cts.Token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            LogMsg($"Refresh feature switches failed: {e.Message}");
        }
    }

    private async Task SendAsync(byte[] packet, CancellationToken ct)
    {
        try
        {
            await _client.SendAsync(packet, ct).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            LogMsg($"Send failed: {e.Message}");
        }
    }

    public async Task SetAncModeAsync(NoiseControlMode mode)
    {
        var packet = Profile.BuildAncPacket(mode);
        AncMode = mode;
        AncModeChanged?.Invoke(this, AncMode);
        await SendAsync(packet, _cts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// 切换 EQ 预设。仅当运行时能力发现确认支持 EQ 时生效。
    /// </summary>
    public async Task SetEqPresetAsync(byte presetId)
    {
        if (!Capabilities.SupportsEq)
        {
            EqPresetId = null;
            EqPresetChanged?.Invoke(this, EqPresetId);
            return;
        }
        EqPresetId = presetId;
        EqPresetChanged?.Invoke(this, EqPresetId);
        var packet = Profile.BuildEqPacket(presetId);
        await SendAsync(packet, _cts.Token).ConfigureAwait(false);
    }

    public async Task SetGameModeAsync(bool enabled)
    {
        if (!Capabilities.SupportsGameMode)
        {
            GameMode = false;
            GameModeChanged?.Invoke(this, GameMode);
            return;
        }
        GameMode = enabled;
        GameModeChanged?.Invoke(this, GameMode);
        var packets = OppoEnums.GameModePackets(enabled, _gameModeImplementation);
        for (int i = 0; i < packets.Count; i++)
        {
            if (i > 0) await Task.Delay(120, _cts.Token).ConfigureAwait(false);
            await SendAsync(packets[i], _cts.Token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 切换空间音频开关（0x0403 载荷 1B 01/00）。仅当能力发现确认支持时生效。
    /// </summary>
    public async Task SetSpatialAudioAsync(bool enabled)
    {
        if (!Capabilities.SupportsSpatialAudio) return;
        var packet = enabled ? OppoEnums.SpatialOn : OppoEnums.SpatialOff;
        await SendAsync(packet, _cts.Token).ConfigureAwait(false);
        LogMsg($"Spatial audio set: {enabled}");
    }

    /// <summary>
    /// 切换双设备连接开关（0x0403 载荷 [0x11, 01/00]）。
    /// 仅当能力发现确认设备声明支持 feature 0x11 时生效。
    /// </summary>
    public async Task SetMultiDeviceAsync(bool enabled)
    {
        if (!Capabilities.SupportsMultiDevice) return;
        var packet = OppoEnums.BuildFeatureSwitchPacket(FeatureId.MULTI_DEVICES_CONNECT, enabled);
        await SendAsync(packet, _cts.Token).ConfigureAwait(false);
        LogMsg($"Multi-device connect set: {enabled}");
        // 设置后刷新功能开关以同步状态
        _ = RefreshFeatureSwitchesAsync();
    }

    public void SetGameModeImplementation(GameModeImplementation implementation)
    {
        _gameModeImplementation = implementation;
    }

    public async Task RefreshStatusAsync()
    {
        if (!_running) return;
        await QueryStatusAsync(_cts.Token).ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        _running = false;
        CancelExisting();
        _client.Disconnect();
        SetState(ConnectionState.Disconnected);
        Battery = BatteryParams.Empty;
        AncMode = NoiseControlMode.Off;
        DeviceName = string.Empty;
        GameMode = false;
        EqPresetId = null;
        Capabilities = new DeviceCapabilities();
        _capabilitiesDiscovered = false;
        Profile = DeviceProfileRegistry.Default;
        BatteryChanged?.Invoke(this, Battery);
        AncModeChanged?.Invoke(this, AncMode);
        GameModeChanged?.Invoke(this, GameMode);
        EqPresetChanged?.Invoke(this, EqPresetId);
        CapabilitiesChanged?.Invoke(this, Capabilities);
        await Task.CompletedTask;
    }

    private void CancelExisting()
    {
        try { _cts.Cancel(); } catch { }
        try { _cts.Dispose(); } catch { }
        _cts = new CancellationTokenSource();
    }

    private void SetState(ConnectionState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private void LogMsg(string message)
    {
        Debug.WriteLine($"[{Tag}] {message}");
        Log?.Invoke(this, message);
    }

    private static string ToHex(byte[] data)
    {
        var sb = new System.Text.StringBuilder(data.Length * 3);
        foreach (var b in data) sb.Append(b.ToString("X2")).Append(' ');
        return sb.ToString().TrimEnd();
    }

    public void Dispose()
    {
        _running = false;
        try { _cts.Cancel(); } catch { }
        _client.Dispose();
        try { _cts.Dispose(); } catch { }
    }
}
