namespace OPods.Pods;

/// <summary>
/// Single battery component reading: level percentage + charging flag.
/// </summary>
public sealed record BatteryInfo(int Level, bool IsCharging);

/// <summary>
/// Aggregated battery result for left/right/case components.
/// Any component may be null if not present in the response.
/// </summary>
public sealed record BatteryResult(BatteryInfo? Left, BatteryInfo? Right, BatteryInfo? Case);

/// <summary>
/// Parser for OPPO earphone battery response packets.
///
/// Response packet format: AA + TotalLen + 0000 + Cmd(0x8106 = 06 81) + Seq + PayLen + Payload
/// Payload consists of pairs: [Index(1B), RawValue(1B)]
///   Index: 1=Left, 2=Right, 3=Case
///   RawValue: battery = value &amp; 0x7F, charging = (value &amp; 0x80) != 0
/// </summary>
public static class BatteryParser
{
    /// <summary>
    /// Parse a raw packet buffer for battery query response (Cmd=0x8106).
    /// Returns null if the packet is not a valid battery response.
    /// </summary>
    public static BatteryResult? Parse(byte[] data)
    {
        if (data.Length < 9) return null;
        if (data[0] != 0xAA) return null;

        int cmd = (data[4] & 0xFF) | ((data[5] & 0xFF) << 8);
        if (cmd != Cmd.BATTERY_RESPONSE) return null;

        int payLen = (data[7] & 0xFF) | ((data[8] & 0xFF) << 8);
        const int payloadStart = 9;
        if (data.Length < payloadStart + payLen) return null;

        BatteryInfo? left = null, right = null, caseInfo = null;

        int i = payloadStart;
        int end = payloadStart + payLen;
        while (i + 1 < end)
        {
            int index = data[i] & 0xFF;
            int rawValue = data[i + 1] & 0xFF;
            int level = rawValue & 0x7F;
            bool charging = (rawValue & 0x80) != 0;
            var info = new BatteryInfo(level, charging);

            switch (index)
            {
                case BatteryComponent.LEFT: left = info; break;
                case BatteryComponent.RIGHT: right = info; break;
                case BatteryComponent.CASE: caseInfo = info; break;
            }
            i += 2;
        }

        return new BatteryResult(left, right, caseInfo);
    }

    /// <summary>
    /// Parse an active/unsolicited battery report (Cmd=0x0204, payload type=0x01).
    ///
    /// Active report format:
    /// Payload[0] = 0x01 (report type: battery)
    /// Payload[1] = count (number of index-value pairs)
    /// Payload[2..] = [Index(1B), StatusValue(1B)] * count
    /// </summary>
    public static BatteryResult? ParseActiveReport(byte[] data)
    {
        if (data.Length < 9) return null;
        if (data[0] != 0xAA) return null;

        int cmd = (data[4] & 0xFF) | ((data[5] & 0xFF) << 8);
        if (cmd != Cmd.ANC_MODE_NOTIFY) return null;

        int payLen = (data[7] & 0xFF) | ((data[8] & 0xFF) << 8);
        const int payloadStart = 9;
        if (data.Length < payloadStart + payLen) return null;
        if (payLen < 2) return null;

        int reportType = data[payloadStart] & 0xFF;
        if (reportType != 0x01) return null;

        int count = data[payloadStart + 1] & 0xFF;
        if (payLen < 2 + count * 2) return null;

        BatteryInfo? left = null, right = null, caseInfo = null;

        for (int j = 0; j < count; j++)
        {
            int idx = payloadStart + 2 + j * 2;
            if (idx + 1 >= data.Length) break;
            int index = data[idx] & 0xFF;
            int rawValue = data[idx + 1] & 0xFF;
            int level = rawValue & 0x7F;
            bool charging = (rawValue & 0x80) != 0;
            var info = new BatteryInfo(level, charging);

            switch (index)
            {
                case BatteryComponent.LEFT: left = info; break;
                case BatteryComponent.RIGHT: right = info; break;
                case BatteryComponent.CASE: caseInfo = info; break;
            }
        }

        return new BatteryResult(left, right, caseInfo);
    }
}
