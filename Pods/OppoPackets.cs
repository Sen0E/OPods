namespace OPods.Pods;

/// <summary>
/// OPPO earphone RFCOMM protocol packet definitions.
/// Packet format (Little Endian for multi-byte fields):
/// Header(AA) + TotalLen(1B) + Res(0000) + Cmd(2B) + Seq(1B) + PayLen(2B) + Payload
/// </summary>
public static class OppoPackets
{
    /// <summary>Build a complete OPPO protocol packet.</summary>
    public static byte[] BuildPacket(int cmd, byte seq = 0xF0, byte[]? payload = null)
    {
        payload ??= Array.Empty<byte>();
        int payLen = payload.Length;
        int totalLen = 7 + payLen;
        var packet = new byte[2 + totalLen];
        packet[0] = 0xAA;
        packet[1] = (byte)totalLen;
        packet[2] = 0x00;
        packet[3] = 0x00;
        packet[4] = (byte)(cmd & 0xFF);
        packet[5] = (byte)((cmd >> 8) & 0xFF);
        packet[6] = seq;
        packet[7] = (byte)(payLen & 0xFF);
        packet[8] = (byte)((payLen >> 8) & 0xFF);
        Buffer.BlockCopy(payload, 0, packet, 9, payLen);
        return packet;
    }
}

/// <summary>
/// Accumulates raw bytes from the RFCOMM stream and emits complete OPPO frames.
/// Mirrors the Kotlin <c>OppoPacketFramer</c>.
/// </summary>
public sealed class OppoPacketFramer
{
    private const byte Header = 0xAA;
    private const int MinTotalLen = 7;
    private const int MaxFrameLen = 512;

    private List<byte> _pending = new();

    /// <summary>Append newly received bytes and return zero or more complete frames.</summary>
    public List<byte[]> Append(byte[] buffer, int length)
    {
        var frames = new List<byte[]>();
        if (length <= 0) return frames;

        if (_pending.Count == 0)
        {
            _pending.AddRange(buffer.AsSpan(0, length).ToArray());
        }
        else
        {
            _pending.AddRange(buffer.AsSpan(0, length).ToArray());
        }

        while (_pending.Count > 0)
        {
            int start = _pending.IndexOf(Header);
            if (start < 0)
            {
                _pending.Clear();
                break;
            }
            if (start > 0)
            {
                _pending.RemoveRange(0, start);
            }
            if (_pending.Count < 2) break;

            int totalLen = _pending[1] & 0xFF;
            int frameLen = totalLen + 2;
            if (totalLen < MinTotalLen || frameLen > MaxFrameLen)
            {
                _pending.RemoveAt(0);
                continue;
            }
            if (_pending.Count < frameLen) break;

            var frame = _pending.GetRange(0, frameLen).ToArray();
            frames.Add(frame);
            _pending.RemoveRange(0, frameLen);
        }

        return frames;
    }

    /// <summary>Reset internal buffer (e.g. after reconnect).</summary>
    public void Reset() => _pending.Clear();
}
