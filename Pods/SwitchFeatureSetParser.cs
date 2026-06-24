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
        if (data.Length < 9) return null;
        if (data[0] != 0xAA) return null;

        int cmd = (data[4] & 0xFF) | ((data[5] & 0xFF) << 8);
        if (cmd != Cmd.SET_GAME_MODE_RESPONSE) return null;

        int payLen = (data[7] & 0xFF) | ((data[8] & 0xFF) << 8);
        const int payloadStart = 9;
        if (payLen <= 0 || data.Length < payloadStart + payLen) return null;

        int status = data[payloadStart] & 0xFF;
        int? value = payLen > 1 ? data[payloadStart + 1] & 0xFF : null;
        return new SwitchFeatureSetResult(status, value);
    }
}
