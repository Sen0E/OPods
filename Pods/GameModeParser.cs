namespace OPods.Pods;

/// <summary>
/// Game-mode status parsed from a batch parameter query response (Cmd=0x810D).
/// Either field may be null if not present in the response.
/// </summary>
public sealed record GameModeStatus(bool? MainEnabled, bool? LowLatencyEnabled)
{
    public bool? EnabledFor(GameModeImplementation implementation) => implementation switch
    {
        // Standard 优先用主开关 0x28；机型仅上报低延迟 0x06 时回退
        GameModeImplementation.Standard => MainEnabled ?? LowLatencyEnabled,
        // Compatible 优先用低延迟 0x06；不存在时回退到主开关
        GameModeImplementation.Compatible => LowLatencyEnabled ?? MainEnabled,
        _ => MainEnabled ?? LowLatencyEnabled
    };
}

/// <summary>
/// Parser for game mode status from batch parameter query response (Cmd=0x810D).
/// 实际解析委托给 <see cref="FeatureSwitchParser"/>；本类只负责从 feature id 映射中
/// 提取 <see cref="FeatureId.GAME_SOUND_MAIN"/>（主开关）和 <see cref="FeatureId.GAME_MODE"/>（低延迟）。
/// </summary>
public static class GameModeParser
{
    public static bool? Parse(byte[] data, GameModeImplementation implementation = GameModeImplementation.Standard)
    {
        return ParseStatus(data)?.EnabledFor(implementation);
    }

    public static GameModeStatus? ParseStatus(byte[] data)
    {
        return FeatureSwitchParser.Parse(data)?.ToGameModeStatus();
    }
}
