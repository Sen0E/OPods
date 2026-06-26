namespace OPods.Pods;

/// <summary>
/// Parser for OPPO earphone EQ preset response/notification packets.
///
/// Cmd: 0x810F (EQ query response / preset change notification)
/// Cmd: 0x0504 (EQ preset 主动通知，seq=0xFF；手机端切换调音时设备推送)
/// Payload layout:
///   0x0504: PayLen &gt;= 1 → [presetId]
///   0x810F: PayLen &gt;= 1 → [status]            (legacy / short form: status byte alone carries preset id)
///   0x810F: PayLen &gt;= 2 → [status, presetId]   (standard 0x810F form)
/// Returns the preset id on success, null otherwise.
/// Mirrors the Python <c>_pe</c> parser in oppopods_ctk.py.
/// </summary>
public static class EqPresetParser
{
    public static byte? Parse(byte[] data)
    {
        if (!OppoPackets.TryGetPacketLayout(data, out var layout)) return null;
        if (layout.PayLen <= 0) return null;

        int p = layout.PayloadOffset;

        // 0x0504 主动通知：payload=[presetId]
        if (layout.Cmd == Cmd.EQ_PRESET_NOTIFY) return (byte)(data[p] & 0xFF);

        if (layout.Cmd != Cmd.EQ_RESPONSE) return null;

        // 标准 0x810F 响应：[status, presetId]，取 presetId
        if (layout.PayLen >= 2) return (byte)(data[p + 1] & 0xFF);

        // 短形式：仅 [presetId]
        return (byte)(data[p] & 0xFF);
    }
}
