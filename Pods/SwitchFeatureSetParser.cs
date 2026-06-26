namespace OPods.Pods;

/// <summary>
/// Result of a switch-feature-set response (Cmd=0x8403).
/// </summary>
public sealed record SwitchFeatureSetResult(int Status, int? Value);

/// <summary>
/// Parser for switch-feature-set response packets (Cmd=0x8403).
/// Used for logging only; does not drive UI state.
/// </summary>
public static class SwitchFeatureSetParser
{
    public static SwitchFeatureSetResult? Parse(byte[] data)
    {
        if (!OppoPackets.TryGetPacketLayout(data, out var layout)) return null;
        if (layout.Cmd != Cmd.SET_GAME_MODE_RESPONSE) return null;
        if (layout.PayLen <= 0) return null;

        int p = layout.PayloadOffset;
        int status = data[p] & 0xFF;
        int? value = layout.PayLen > 1 ? (data[p + 1] & 0xFF) : null;
        return new SwitchFeatureSetResult(status, value);
    }
}
