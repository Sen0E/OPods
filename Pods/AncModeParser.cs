namespace OPods.Pods;

/// <summary>
/// Parser for OPPO earphone ANC mode response/notification packets.
///
/// Cmd: 0x810C (mode query response) or 0x0204 (mode change notification)
/// Scan payload for consecutive bytes 01 01 [Val1] [Val2]
/// Val mapping: 0x10 0x00=NC, 0x00 0x01=Transparency, 0x08 0x00=Off, 0x00 0x08=Adaptive
/// </summary>
public static class AncModeParser
{
    public static NoiseControlMode? Parse(byte[] data)
    {
        if (data.Length < 9) return null;
        if (data[0] != 0xAA) return null;

        int cmd = (data[4] & 0xFF) | ((data[5] & 0xFF) << 8);
        if (cmd != Cmd.ANC_MODE_RESPONSE && cmd != Cmd.ANC_MODE_NOTIFY) return null;

        int payLen = (data[7] & 0xFF) | ((data[8] & 0xFF) << 8);
        const int payloadStart = 9;
        if (data.Length < payloadStart + payLen) return null;

        // For 0x0204, skip if this is a battery report (type=0x01) or button report (type=0x02)
        if (cmd == Cmd.ANC_MODE_NOTIFY && payLen > 0)
        {
            int reportType = data[payloadStart] & 0xFF;
            if (reportType == 0x01 || reportType == 0x02) return null;
        }

        int scanEnd = Math.Min(payloadStart + payLen - 3, data.Length - 3);
        for (int i = payloadStart; i <= scanEnd; i++)
        {
            if (data[i] == 0x01 && data[i + 1] == 0x01)
            {
                int val1 = data[i + 2] & 0xFF;
                int val2 = data[i + 3] & 0xFF;

                if (val1 == 0x10 && val2 == 0x00) return NoiseControlMode.NoiseCancellation;
                if (val1 == 0x00 && val2 == 0x01) return NoiseControlMode.Transparency;
                if (val1 == 0x08 && val2 == 0x00) return NoiseControlMode.Off;
                if (val1 == 0x00 && val2 == 0x08) return NoiseControlMode.Adaptive;
                return null;
            }
        }
        return null;
    }
}
