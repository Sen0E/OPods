namespace OPods.Pods;

/// <summary>
/// Parser for OPPO earphone ANC mode response/notification packets.
///
/// Cmd: 0x810C (mode query response) 或 0x0204 eventCode=0x03（模式变化通知）。
/// 0x810C payload 扫描连续字节 01 01 [Val1] [Val2]；0x0204 事件先经
/// <see cref="NotificationEventParser"/> 分流，eventCode=0x03 子 payload 形态同 0x810C。
/// (v1,v2) → 模式 的映射表来自传入的 <see cref="DeviceProfile.AncResponseMap"/>，不再硬编码。
/// </summary>
public static class AncModeParser
{
    public static NoiseControlMode? Parse(byte[] data, DeviceProfile profile)
    {
        if (!OppoPackets.TryGetPacketLayout(data, out var layout)) return null;

        var map = profile.AncResponseMap;

        if (layout.Cmd == Cmd.ANC_MODE_RESPONSE)
        {
            return ScanAncPattern(data, layout.PayloadOffset, layout.PayLen, map);
        }

        if (layout.Cmd == Cmd.NOTIF_EVENT)
        {
            // 0x0204 主动事件：电量(0x01)/佩戴(0x02) 交给各自解析器，仅处理 ANC(0x03) 及
            // 其它未识别事件（保持与重构前一致的扫描行为：在整段 payload 中找 01 01 v1 v2）。
            if (layout.PayLen <= 0) return null;
            int eventCode = data[layout.PayloadOffset] & 0xFF;
            if (eventCode == NotificationEventCode.BATTERY
                || eventCode == NotificationEventCode.WEAR_STATUS) return null;

            return ScanAncPattern(data, layout.PayloadOffset, layout.PayLen, map);
        }

        return null;
    }

    /// <summary>在 [start, start+len) 区间扫描 01 01 [v1] [v2] 模式码。</summary>
    private static NoiseControlMode? ScanAncPattern(byte[] data, int start, int len,
        IReadOnlyDictionary<(int, int), NoiseControlMode> map)
    {
        if (len <= 0) return null;
        int end = start + len;
        if (data.Length < end) return null;

        int scanEnd = end - 3;
        for (int i = start; i <= scanEnd; i++)
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
