namespace OPods.Pods;

/// <summary>
/// 编解码器类型码与友好名映射。
/// 来源：AOSP BluetoothCodecType 常见取值（协议文档未明确枚举，按通用蓝牙编解码器约定）。
/// </summary>
public static class CodecType
{
    public const int SBC = 0;
    public const int AAC = 1;
    public const int APTX = 2;
    public const int APTX_HD = 3;
    public const int LDAC = 4;
    public const int LHDC = 5;
    public const int LHDC_V5 = 6;
    public const int SCALENDAR = 7;

    /// <summary>将编解码器类型码映射为友好名；未知码返回 "Unknown(0xNN)"。</summary>
    public static string ToName(int codecType) => codecType switch
    {
        SBC => "SBC",
        AAC => "AAC",
        APTX => "aptX",
        APTX_HD => "aptX HD",
        LDAC => "LDAC",
        LHDC => "LHDC",
        LHDC_V5 => "LHDC V5",
        SCALENDAR => "Scalendar",
        _ => $"Unknown(0x{codecType:X2})"
    };
}

/// <summary>
/// 解析当前编解码器查询响应（Cmd=0x8114）。
/// 协议文档未明确 payload 布局，按 [status, codecType] 或单字节 [codecType] 防御性解析。
/// </summary>
public static class CodecParser
{
    /// <summary>
    /// 解析 0x8114 响应，返回编解码器类型码；非该命令或 payload 为空返回 null。
    /// </summary>
    public static int? Parse(byte[] data)
    {
        if (!OppoPackets.TryGetPacketLayout(data, out var layout)) return null;
        if (layout.Cmd != Cmd.CODEC_RESPONSE) return null;
        if (layout.PayLen <= 0) return null;

        int p = layout.PayloadOffset;
        // 标准 [status, codecType] 形式
        if (layout.PayLen >= 2) return data[p + 1] & 0xFF;
        // 短形式 [codecType]
        return data[p] & 0xFF;
    }

    /// <summary>解析并返回友好名；失败返回 null。</summary>
    public static string? ParseName(byte[] data) => Parse(data) is { } t ? CodecType.ToName(t) : null;
}
