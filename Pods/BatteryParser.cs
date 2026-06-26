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
        if (!OppoPackets.TryGetPacketLayout(data, out var layout)) return null;
        if (layout.Cmd != Cmd.BATTERY_RESPONSE) return null;

        return ParsePairs(data, layout.PayloadOffset, layout.PayLen);
    }

    /// <summary>
    /// Parse an active/unsolicited battery report (Cmd=0x0204, eventCode=0x01).
    /// 实际分流统一走 <see cref="NotificationEventParser"/>；本方法保留为兼容入口。
    /// </summary>
    public static BatteryResult? ParseActiveReport(byte[] data)
    {
        var ev = NotificationEventParser.Parse(data);
        if (ev is null || ev.EventCode != NotificationEventCode.BATTERY) return null;
        return ev.Battery;
    }

    /// <summary>扫描 [index, rawValue] 对构造 BatteryResult。</summary>
    internal static BatteryResult? ParsePairs(byte[] data, int payloadStart, int payLen)
    {
        if (payLen <= 0) return null;
        int end = payloadStart + payLen;
        if (data.Length < end) return null;

        BatteryInfo? left = null, right = null, caseInfo = null;
        int i = payloadStart;
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
}
