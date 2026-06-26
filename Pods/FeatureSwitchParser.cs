namespace OPods.Pods;

/// <summary>
/// 功能开关批量查询响应（Cmd=0x810D）的解析结果。
/// 包含响应中所有 (featureId, enabled) 对，并提供按 feature id 查询的辅助方法。
/// </summary>
public sealed class FeatureSwitchStatus
{
    private readonly Dictionary<int, bool> _map;

    internal FeatureSwitchStatus(Dictionary<int, bool> map) => _map = map;

    /// <summary>响应中包含的所有 feature id。</summary>
    public IEnumerable<int> FeatureIds => _map.Keys;

    /// <summary>feature id 数量。</summary>
    public int Count => _map.Count;

    /// <summary>查询指定 feature id 的开关状态；不存在返回 null。</summary>
    public bool? Get(int featureId) => _map.TryGetValue(featureId, out var v) ? v : null;

    /// <summary>是否包含指定 feature id。</summary>
    public bool Contains(int featureId) => _map.ContainsKey(featureId);

    /// <summary>
    /// 获取游戏模式聚合状态。
    /// MainEnabled = feature <see cref="FeatureId.GAME_SOUND_MAIN"/> 的开关
    /// LowLatencyEnabled = feature <see cref="FeatureId.GAME_MODE"/> 的开关
    /// </summary>
    public GameModeStatus ToGameModeStatus()
    {
        bool? main = Get(FeatureId.GAME_SOUND_MAIN);
        bool? lowLatency = Get(FeatureId.GAME_MODE);
        return new GameModeStatus(main, lowLatency);
    }
}

/// <summary>
/// 解析 0x810D 批量功能开关查询响应，兼容 varint TotalLen 与两种 payload 布局：
/// 1) 结构化：[status=0x00] [count] ([featureId] [value]) * count
/// 2) 扁平 legacy：([featureId] [value]) * 直接成对扫描
/// 值字节仅接受 0x00 / 0x01，其它视为非法并跳过该对。
/// </summary>
public static class FeatureSwitchParser
{
    public static FeatureSwitchStatus? Parse(byte[] data)
    {
        if (!OppoPackets.TryGetPacketLayout(data, out var layout)) return null;
        if (layout.Cmd != Cmd.QUERY_STATUS_RESPONSE) return null;
        if (layout.PayLen <= 0) return null;

        int p = layout.PayloadOffset;
        int end = p + layout.PayLen;
        if (data.Length < end) return null;

        var map = new Dictionary<int, bool>();

        // 结构化形式：[0x00] [count] ([featureId] [value]) * count
        if (layout.PayLen >= 2
            && (data[p] & 0xFF) == 0x00)
        {
            int count = data[p + 1] & 0xFF;
            if (count > 0 && layout.PayLen >= 2 + count * 2)
            {
                for (int j = 0; j < count; j++)
                {
                    int idx = p + 2 + j * 2;
                    int featureId = data[idx] & 0xFF;
                    int value = data[idx + 1] & 0xFF;
                    if (value == 0x00 || value == 0x01)
                        map[featureId] = value == 0x01;
                }
                if (map.Count > 0) return new FeatureSwitchStatus(map);
            }
        }

        // 扁平 legacy 形式：直接成对扫描
        int scanEnd = end - 1;
        for (int i = p; i <= scanEnd; i++)
        {
            int featureId = data[i] & 0xFF;
            int value = data[i + 1] & 0xFF;
            if (value != 0x00 && value != 0x01) continue;
            map[featureId] = value == 0x01;
            i++; // 跳过 value 字节
        }

        return map.Count > 0 ? new FeatureSwitchStatus(map) : null;
    }
}
