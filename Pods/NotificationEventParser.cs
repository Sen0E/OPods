namespace OPods.Pods;

/// <summary>已知 0x0204 主动事件 eventCode。</summary>
public static class NotificationEventCode
{
    public const int BATTERY = 0x01;
    public const int WEAR_STATUS = 0x02;
    public const int ANC = 0x03;
    public const int COMPACTNESS = 0x04;
    public const int GAME_MODE = 0x05;
    public const int MULTI_DEVICE = 0x06;
    public const int ZEN_MODE = 0x0A;
    public const int PERSONAL_NOISE = 0x0B;
    public const int TRIANGLE = 0x0D;
    public const int EAR_SCAN = 0x0E;
    public const int PUBLIC_MASK = 0x0F;
    public const int ONESHOT = 0x10;
    public const int USER_INTERACTION = 0xF1;
    public const int CONNECTED_DEVICES = 0xF2;
    public const int JSON_DIAG = 0xF4;
}

/// <summary>
/// 0x0204 主动事件的解析结果。按 eventCode 分流，仅填充对应字段，其余为 null。
/// 已实现分流：电量、佩戴状态。其它 eventCode 以原始 payload 透传（<see cref="RawPayload"/>）供扩展。
/// </summary>
public sealed record NotificationEvent(
    int EventCode,
    BatteryResult? Battery,
    WearStatus? Wear,
    byte[]? RawPayload)
{
    /// <summary>事件类型友好名。</summary>
    public string EventName => EventCode switch
    {
        NotificationEventCode.BATTERY => "Battery",
        NotificationEventCode.WEAR_STATUS => "WearStatus",
        NotificationEventCode.ANC => "ANC",
        NotificationEventCode.GAME_MODE => "GameMode",
        NotificationEventCode.ZEN_MODE => "ZenMode",
        _ => $"0x{EventCode:X2}"
    };
}

/// <summary>
/// 统一解析 0x0204 主动事件包，按 eventCode 分流到电量/佩戴等子解析器。
/// payload 形态：&lt;eventCode:1&gt; &lt;count:1&gt; &lt;eventData...&gt;（电量/佩戴为 [comp,value] 对）。
/// 替代 BatteryParser.ParseActiveReport / AncModeParser 对 0x0204 的重复判别。
/// </summary>
public static class NotificationEventParser
{
    /// <summary>
    /// 解析 0x0204 帧。非 0x0204 或 payload 为空返回 null。
    /// </summary>
    public static NotificationEvent? Parse(byte[] data)
    {
        if (!OppoPackets.TryGetPacketLayout(data, out var layout)) return null;
        if (layout.Cmd != Cmd.NOTIF_EVENT) return null;
        if (layout.PayLen <= 0) return null;

        int p = layout.PayloadOffset;
        int end = p + layout.PayLen;
        if (data.Length < end) return null;

        int eventCode = data[p] & 0xFF;

        // 截取 eventCode 之后的子 payload，复用现有按 eventCode 的子解析器
        int subLen = layout.PayLen - 1;
        byte[] sub = new byte[subLen];
        if (subLen > 0) Buffer.BlockCopy(data, p + 1, sub, 0, subLen);

        switch (eventCode)
        {
            case NotificationEventCode.BATTERY:
                {
                    var bat = ParseBatteryPayload(sub, subLen);
                    return new NotificationEvent(eventCode, bat, null, null);
                }
            case NotificationEventCode.WEAR_STATUS:
                {
                    // WearStatusParser.ParsePayload 期望起始是 eventCode，这里 sub 已去掉 eventCode，
                    // 故用 count+ pairs 形式直接解析。
                    var wear = ParseWearPayload(sub, subLen);
                    return new NotificationEvent(eventCode, null, wear, null);
                }
            default:
                // 其它 eventCode 暂不解析，透传原始 payload 供后续扩展
                return new NotificationEvent(eventCode, null, null, sub);
        }
    }

    /// <summary>解析电量事件子 payload：&lt;count&gt; ([comp] [rawValue]) * count。</summary>
    private static BatteryResult? ParseBatteryPayload(byte[] sub, int len)
    {
        if (len < 1) return null;
        int count = sub[0] & 0xFF;
        if (len < 1 + count * 2) return null;

        BatteryInfo? left = null, right = null, caseInfo = null;
        for (int j = 0; j < count; j++)
        {
            int i = 1 + j * 2;
            int index = sub[i] & 0xFF;
            int raw = sub[i + 1] & 0xFF;
            int level = raw & 0x7F;
            bool charging = (raw & 0x80) != 0;
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

    /// <summary>解析佩戴事件子 payload：&lt;count&gt; ([comp] [status]) * count。</summary>
    private static WearStatus? ParseWearPayload(byte[] sub, int len)
    {
        if (len < 1) return null;
        int count = sub[0] & 0xFF;
        if (len < 1 + count * 2) return null;

        WearState left = WearState.Unknown, right = WearState.Unknown;
        for (int j = 0; j < count; j++)
        {
            int i = 1 + j * 2;
            int comp = sub[i] & 0xFF;
            int st = sub[i + 1] & 0xFF;
            var state = WearStatusParser.DecodeState(st);
            switch (comp)
            {
                case BatteryComponent.LEFT: left = state; break;
                case BatteryComponent.RIGHT: right = state; break;
            }
        }
        return new WearStatus(left, right);
    }
}

/// <summary>通知注册响应（0x8201 / 0x8205）解析结果。</summary>
public sealed record NotificationRegisterResult(int Status, int RegisteredCount);

/// <summary>解析 0x8201 / 0x8205 注册响应。payload：[status] [count?]...</summary>
public static class NotificationRegisterParser
{
    public static NotificationRegisterResult? Parse(byte[] data)
    {
        if (!OppoPackets.TryGetPacketLayout(data, out var layout)) return null;
        if (layout.Cmd != Cmd.NOTIF_REGISTER_RESPONSE
            && layout.Cmd != Cmd.NOTIF_REGISTER_MULTI_RESPONSE) return null;
        if (layout.PayLen <= 0) return null;

        int p = layout.PayloadOffset;
        int status = data[p] & 0xFF;
        int count = layout.PayLen >= 2 ? (data[p + 1] & 0xFF) : 0;
        return new NotificationRegisterResult(status, count);
    }
}
