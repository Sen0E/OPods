namespace OPods.Pods;

/// <summary>
/// Parser for OPPO earphone ANC mode response/notification packets.
///
/// Cmd: 0x810C (mode query response) or 0x0204 (mode change notification)
/// Scan payload for consecutive bytes 01 01 [Val1] [Val2]
/// (v1,v2) → 模式的映射表来自传入的 <see cref="DeviceProfile.AncResponseMap"/>，不再硬编码。
/// </summary>
public static class AncModeParser
{
    public static NoiseControlMode? Parse(byte[] data, DeviceProfile profile)
    {
        if (data.Length < 9) return null;
        if (data[0] != 0xAA) return null;

        int cmd = (data[4] & 0xFF) | ((data[5] & 0xFF) << 8);
        if (cmd != Cmd.ANC_MODE_RESPONSE && cmd != Cmd.ANC_MODE_NOTIFY) return null;

        int payLen = (data[7] & 0xFF) | ((data[8] & 0xFF) << 8);
        const int payloadStart = 9;
        if (data.Length < payloadStart + payLen) return null;

        // For 0x0204, skip if this is a battery report (type=0x01) or button report (type=0x02)
        if (cmd == Cmd.ANC_MODE_NOTIFY && payLen > 0)
        {
            int reportType = data[payloadStart] & 0xFF;
            if (reportType == 0x01 || reportType == 0x02) return null;
        }

        var map = profile.AncResponseMap;

        int scanEnd = Math.Min(payloadStart + payLen - 3, data.Length - 3);
        for (int i = payloadStart; i <= scanEnd; i++)
        {
            if (data[i] == 0x01 && data[i + 1] == 0x01)
            {
                int val1 = data[i + 2] & 0xFF;
                int val2 = data[i + 3] & 0xFF;
                return map.TryGetValue((val1, val2), out var mode) ? mode : null;
            }
        }
        return null;
    }
}
