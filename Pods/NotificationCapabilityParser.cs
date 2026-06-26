namespace OPods.Pods;

/// <summary>通知能力响应（0x8200）的解析结果：支持的事件码列表。</summary>
public sealed record NotificationCapability(IReadOnlyList<int> SupportedEventCodes)
{
    /// <summary>设备是否支持指定 eventCode。</summary>
    public bool Supports(int eventCode) => SupportedEventCodes.Contains(eventCode);

    /// <summary>设备是否支持批量注册（0x0205）；官方协议约定 0x0205 在能力列表中即表示支持。</summary>
    public bool SupportsMultiRegister => SupportedEventCodes.Contains(Cmd.NOTIF_REGISTER_MULTI & 0xFF);
}

/// <summary>
/// 解析 0x8200 通知能力响应。
/// payload 形态：&lt;status:1&gt; &lt;count:1&gt; &lt;eventCode:1&gt;... （status=0x00 表示成功）。
/// 来源：PROMPT_PROTOCOL_REFACTOR.md 第 4.4 节。
/// </summary>
public static class NotificationCapabilityParser
{
    public static NotificationCapability? Parse(byte[] data)
    {
        if (!OppoPackets.TryGetPacketLayout(data, out var layout)) return null;
        if (layout.Cmd != Cmd.NOTIF_CAPABILITY_RESPONSE) return null;
        if (layout.PayLen < 2) return null;

        int p = layout.PayloadOffset;
        int count = data[p + 1] & 0xFF;
        if (layout.PayLen < 2 + count) return null;

        var codes = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            codes.Add(data[p + 2 + i] & 0xFF);
        }
        return new NotificationCapability(codes);
    }
}
