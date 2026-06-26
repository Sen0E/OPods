namespace OPods.Pods;

/// <summary>
/// 解析远端能力查询响应（Cmd=0x8100）。
/// payload 为小端字节序的能力位掩码（每 bit 对应一项能力）。
/// 协议文档未给出 bit 位定义，本解析器仅保留原始位掩码供后续扩展使用。
/// </summary>
public sealed record RemoteCapability(ulong Bitmask)
{
    /// <summary>查询指定位是否被设备声明支持。</summary>
    public bool IsSet(int bit) => bit is >= 0 and < 64 && (Bitmask & (1UL << bit)) != 0;
}

/// <summary>
/// 解析 0x8100 响应，将 payload 视为最多 8 字节的小端 u64 位掩码。
/// </summary>
public static class CapabilityParser
{
    public static RemoteCapability? Parse(byte[] data)
    {
        if (!OppoPackets.TryGetPacketLayout(data, out var layout)) return null;
        if (layout.Cmd != Cmd.CAPABILITY_RESPONSE) return null;
        if (layout.PayLen <= 0) return null;

        int p = layout.PayloadOffset;
        int n = Math.Min(layout.PayLen, 8);
        ulong mask = 0;
        for (int i = 0; i < n; i++)
        {
            mask |= ((ulong)(data[p + i] & 0xFF)) << (i * 8);
        }
        return new RemoteCapability(mask);
    }
}
