namespace OPods.Pods;

/// <summary>
/// Parser for OPPO earphone EQ preset response/notification packets.
///
/// Cmd: 0x810F (EQ query response / preset change notification)
/// Payload layout:
///   PayLen &gt;= 1 → [status]            (legacy / short form: status byte alone carries preset id)
///   PayLen &gt;= 2 → [status, presetId]   (standard 0x810F form)
/// Returns the preset id on success, null otherwise.
/// Mirrors the Python <c>_pe</c> parser in oppopods_ctk.py.
/// </summary>
public static class EqPresetParser
{
    public static byte? Parse(byte[] data)
    {
        if (!OppoPackets.TryGetPacketLayout(data, out var layout)) return null;
        if (layout.Cmd != Cmd.EQ_RESPONSE) return null;
        if (layout.PayLen <= 0) return null;

        int p = layout.PayloadOffset;

        // 标准 0x810F 响应：[status, presetId]，取 presetId
        if (layout.PayLen >= 2) return (byte)(data[p + 1] & 0xFF);

        // 短形式：仅 [presetId]
        return (byte)(data[p] & 0xFF);
    }
}
