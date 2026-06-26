namespace OPods.Pods;

/// <summary>
/// 解析空间音频类型查询响应（Cmd=0x812A）。
/// 官方日志显示 spatialType 是单字节类型码（0 通常表示关闭/不支持）。
/// 协议文档未明确 payload 布局，按 [status, type] 或单字节 [type] 防御性解析。
/// </summary>
public static class SpatialTypeParser
{
    /// <summary>
    /// 解析 0x812A 响应，返回空间音频类型码；非该命令或 payload 为空返回 null。
    /// 返回值 0 通常表示关闭或不支持，> 0 表示具体空间音频类型。
    /// </summary>
    public static int? Parse(byte[] data)
    {
        if (!OppoPackets.TryGetPacketLayout(data, out var layout)) return null;
        if (layout.Cmd != Cmd.SPATIAL_TYPE_RESPONSE) return null;
        if (layout.PayLen <= 0) return null;

        int p = layout.PayloadOffset;
        // 标准 [status, type] 形式
        if (layout.PayLen >= 2) return data[p + 1] & 0xFF;
        // 短形式 [type]
        return data[p] & 0xFF;
    }
}
