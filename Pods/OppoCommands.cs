namespace OPods.Pods;

/// <summary>Protocol command codes (little-endian on the wire).</summary>
public static class Cmd
{
    public const int SET_ANC = 0x0404;
    public const int SET_GAME_MODE = 0x0403;
    public const int QUERY_BATTERY = 0x0106;
    public const int BATTERY_RESPONSE = 0x8106;
    public const int QUERY_ANC_MODE = 0x010C;
    public const int ANC_MODE_RESPONSE = 0x810C;
    public const int ANC_MODE_NOTIFY = 0x0204;
    public const int QUERY_STATUS = 0x010D;
    public const int QUERY_STATUS_RESPONSE = 0x810D;
    public const int SET_GAME_MODE_RESPONSE = 0x8403;
    public const int SET_EQ = 0x0406;
    public const int QUERY_EQ = 0x010F;
    public const int EQ_RESPONSE = 0x810F;
}

/// <summary>Battery component index in response payload.</summary>
public static class BatteryComponent
{
    public const int LEFT = 1;
    public const int RIGHT = 2;
    public const int CASE = 3;
}

/// <summary>Feature IDs used by the switch-feature command/query.</summary>
public static class GameModeFeature
{
    public const int LOW_LATENCY = 0x06;
    public const int MAIN = 0x28;
}
