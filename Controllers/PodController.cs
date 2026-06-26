using System.Diagnostics;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using OPods.Bluetooth;
using OPods.Pods;

namespace OPods.Controllers;

/// <summary>
/// Standalone RFCOMM controller for OPPO earphones. Owns the connection state
/// machine, background packet reader, 30s battery polling, and command dispatch.
/// Mirrors the Kotlin <c>AppRfcommController</c>.
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

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public BatteryParams Battery { get; private set; } = BatteryParams.Empty;
    public NoiseControlMode AncMode { get; private set; } = NoiseControlMode.Off;
    public bool GameMode { get; private set; }
    public string DeviceName { get; private set; } = string.Empty;

    /// <summary>
    /// 当前 EQ 预设 id（来自耳机上报 / 查询响应）。null 表示未知或不支持。
    /// </summary>
    public byte? EqPresetId { get; private set; }

    /// <summary>当前连接机型的配置，未连接时为 <see cref="DeviceProfileRegistry.Default"/>。</summary>
    public DeviceProfile Profile { get; private set; } = DeviceProfileRegistry.Default;

    public event EventHandler<ConnectionState>? StateChanged;
    public event EventHandler<BatteryParams>? BatteryChanged;
    public event EventHandler<NoiseControlMode>? AncModeChanged;
    public event EventHandler<bool>? GameModeChanged;
    public event EventHandler<byte?>? EqPresetChanged;
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

        var activeResult = BatteryParser.ParseActiveReport(packet);
        if (activeResult != null)
        {
            var current = Battery;
            Battery = new BatteryParams
            {
                Left = MergePodParams(current.Left, activeResult.Left),
                Right = MergePodParams(current.Right, activeResult.Right),
                Case = MergePodParams(current.Case, activeResult.Case)
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
            return;
        }

        var eqResult = EqPresetParser.Parse(packet);
        if (eqResult != null)
        {
            LogMsg($"EQ preset received: id={eqResult}");
            EqPresetId = eqResult;
            EqPresetChanged?.Invoke(this, EqPresetId);
            return;
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

    private async Task QueryStatusAsync(CancellationToken ct)
    {
        try
        {
            await SendAsync(OppoEnums.QueryStatus, ct).ConfigureAwait(false);
            await Task.Delay(50, ct).ConfigureAwait(false);
            await SendAsync(OppoEnums.QueryBattery, ct).ConfigureAwait(false);
            await Task.Delay(50, ct).ConfigureAwait(false);
            await SendAsync(OppoEnums.QueryAnc, ct).ConfigureAwait(false);
            if (Profile.SupportsEq)
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
    /// 切换 EQ 预设。仅当当前 profile 支持 EQ 时生效。
    /// </summary>
    public async Task SetEqPresetAsync(byte presetId)
    {
        if (!Profile.SupportsEq)
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
        if (!Profile.SupportsGameMode)
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
        Profile = DeviceProfileRegistry.Default;
        BatteryChanged?.Invoke(this, Battery);
        AncModeChanged?.Invoke(this, AncMode);
        GameModeChanged?.Invoke(this, GameMode);
        EqPresetChanged?.Invoke(this, EqPresetId);
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
