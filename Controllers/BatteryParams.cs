namespace OPods.Controllers;

/// <summary>Single earphone/case battery component state.</summary>
public sealed class PodParams
{
    public int Battery { get; set; }
    public bool IsCharging { get; set; }
    public bool IsConnected { get; set; }
    public int RawStatus { get; set; }
}

/// <summary>Aggregated battery state for left/right/case.</summary>
public sealed class BatteryParams
{
    public PodParams? Left { get; set; }
    public PodParams? Right { get; set; }
    public PodParams? Case { get; set; }

    public static BatteryParams Empty => new();
}

/// <summary>Controller connection state machine.</summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}
