namespace OPods.Pods;

/// <summary>
/// Game-mode status parsed from a batch parameter query response (Cmd=0x810D).
/// Either field may be null if not present in the response.
/// </summary>
public sealed record GameModeStatus(bool? MainEnabled, bool? LowLatencyEnabled)
{
    public bool? EnabledFor(GameModeImplementation implementation) => implementation switch
    {
        GameModeImplementation.Standard => MainEnabled,
        GameModeImplementation.Compatible => LowLatencyEnabled ?? MainEnabled,
        _ => MainEnabled
    };
}

/// <summary>
/// Parser for game mode status from batch parameter query response (Cmd=0x810D).
/// </summary>
public static class GameModeParser
{
    public static bool? Parse(byte[] data, GameModeImplementation implementation = GameModeImplementation.Standard)
    {
        return ParseStatus(data)?.EnabledFor(implementation);
    }

    public static GameModeStatus? ParseStatus(byte[] data)
    {
        if (data.Length < 9) return null;
        if (data[0] != 0xAA) return null;

        int cmd = (data[4] & 0xFF) | ((data[5] & 0xFF) << 8);
        if (cmd != Cmd.QUERY_STATUS_RESPONSE) return null;

        int payLen = (data[7] & 0xFF) | ((data[8] & 0xFF) << 8);
        const int payloadStart = 9;
        if (data.Length < payloadStart + payLen) return null;

        var structured = ParseStructuredFeaturePairs(data, payloadStart, payLen);
        if (structured != null) return structured;

        bool? mainEnabled = null;
        bool? lowLatencyEnabled = null;
        int scanEnd = Math.Min(payloadStart + payLen - 1, data.Length - 1);
        for (int i = payloadStart; i <= scanEnd; i++)
        {
            int value = data[i + 1] & 0xFF;
            if (value != 0x00 && value != 0x01) continue;
            switch (data[i] & 0xFF)
            {
                case GameModeFeature.MAIN: mainEnabled = value == 0x01; break;
                case GameModeFeature.LOW_LATENCY: lowLatencyEnabled = value == 0x01; break;
            }
        }
        return (mainEnabled != null || lowLatencyEnabled != null)
            ? new GameModeStatus(mainEnabled, lowLatencyEnabled)
            : null;
    }

    private static GameModeStatus? ParseStructuredFeaturePairs(byte[] data, int payloadStart, int payLen)
    {
        if (payLen < 2) return null;

        int statusByte = data[payloadStart] & 0xFF;
        int count = data[payloadStart + 1] & 0xFF;
        if (statusByte != 0x00 || count <= 0 || payLen < 2 + count * 2) return null;

        bool? mainEnabled = null;
        bool? lowLatencyEnabled = null;
        for (int j = 0; j < count; j++)
        {
            int index = payloadStart + 2 + j * 2;
            int featureId = data[index] & 0xFF;
            bool enabled = (data[index + 1] & 0xFF) == 0x01;
            switch (featureId)
            {
                case GameModeFeature.MAIN: mainEnabled = enabled; break;
                case GameModeFeature.LOW_LATENCY: lowLatencyEnabled = enabled; break;
            }
        }
        return (mainEnabled != null || lowLatencyEnabled != null)
            ? new GameModeStatus(mainEnabled, lowLatencyEnabled)
            : null;
    }
}
