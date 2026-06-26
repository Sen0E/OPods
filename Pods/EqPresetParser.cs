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
        if (data.Length < 9) return null;
        if (data[0] != 0xAA) return null;

        int cmd = (data[4] & 0xFF) | ((data[5] & 0xFF) << 8);
        if (cmd != Cmd.EQ_RESPONSE) return null;

        int payLen = (data[7] & 0xFF) | ((data[8] & 0xFF) << 8);
        const int payloadStart = 9;
        if (data.Length < payloadStart + payLen) return null;

        // 标准 0x810F 响应：[status, presetId]，取 presetId
        if (payLen >= 2) return (byte)(data[payloadStart + 1] & 0xFF);

        // 短形式：仅 [presetId]
        if (payLen >= 1) return (byte)(data[payloadStart] & 0xFF);

        return null;
    }
}
