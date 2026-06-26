namespace OPods.Pods;

/// <summary>单耳佩戴状态枚举。</summary>
public enum WearState
{
    /// <summary>未知（响应缺失或 status 码未识别）。</summary>
    Unknown = 0,

    /// <summary>断开/未连接（status 0x00）。</summary>
    Disconnected,

    /// <summary>在充电盒中（status 0x04）。</summary>
    InCase,

    /// <summary>已取出但未佩戴（status 0x05）。</summary>
    Removed,

    /// <summary>佩戴中（status 0x07）。</summary>
    Wearing
}

/// <summary>左右耳佩戴状态聚合。</summary>
public sealed record WearStatus(WearState Left, WearState Right)
{
    /// <summary>任意一耳佩戴中即为 true。</summary>
    public bool AnyWearing => Left == WearState.Wearing || Right == WearState.Wearing;
}

/// <summary>
/// 解析佩戴状态事件（0x0204 eventCode 0x02）。
/// payload 形态：&lt;type=0x02&gt; &lt;count&gt; ([comp] [status]) * count
/// comp: 1=左 / 2=右（3=盒，本场景忽略）；status: 0=disc, 4=in-case, 5=removed, 7=wearing。
/// 来源：GI/PY/oppopods_ctk.py _pn() 中 t==2 分支。
/// </summary>
public static class WearStatusParser
{
    /// <summary>
    /// 从 0x0204 事件的 payload（已跳过外层包头）解析佩戴状态。
    /// <paramref name="payload"/> 起始即 eventCode（0x02）。
    /// </summary>
    public static WearStatus? ParsePayload(byte[] payload, int offset, int length)
    {
        if (length < 2) return null;
        int type = payload[offset] & 0xFF;
        if (type != 0x02) return null;

        int count = payload[offset + 1] & 0xFF;
        if (length < 2 + count * 2) return null;

        WearState left = WearState.Unknown, right = WearState.Unknown;
        for (int j = 0; j < count; j++)
        {
            int i = offset + 2 + j * 2;
            int comp = payload[i] & 0xFF;
            int st = payload[i + 1] & 0xFF;
            var state = DecodeState(st);
            switch (comp)
            {
                case BatteryComponent.LEFT: left = state; break;
                case BatteryComponent.RIGHT: right = state; break;
            }
        }
        return new WearStatus(left, right);
    }

    /// <summary>状态码 → 枚举。</summary>
    public static WearState DecodeState(int status) => status switch
    {
        0x00 => WearState.Disconnected,
        0x04 => WearState.InCase,
        0x05 => WearState.Removed,
        0x07 => WearState.Wearing,
        _ => WearState.Unknown
    };
}
