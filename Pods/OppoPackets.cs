namespace OPods.Pods;

/// <summary>
/// OPPO earphone RFCOMM protocol packet definitions.
/// Packet format (Little Endian for multi-byte fields):
/// Header(AA) + TotalLen(varint 7-bit LE) + Res(0000) + Cmd(2B) + Seq(1B) + PayLen(2B) + Payload
/// </summary>
public static class OppoPackets
{
    /// <summary>Build a complete OPPO protocol packet.</summary>
    public static byte[] BuildPacket(int cmd, byte seq = 0xF0, byte[]? payload = null)
    {
        payload ??= Array.Empty<byte>();
        int payLen = payload.Length;
        int totalLen = 7 + payLen;
        byte[] lenBytes = EncodeVarint(totalLen);
        var packet = new byte[1 + lenBytes.Length + totalLen];
        int off = 0;
        packet[off++] = 0xAA;
        Buffer.BlockCopy(lenBytes, 0, packet, off, lenBytes.Length);
        off += lenBytes.Length;
        packet[off++] = 0x00;
        packet[off++] = 0x00;
        packet[off++] = (byte)(cmd & 0xFF);
        packet[off++] = (byte)((cmd >> 8) & 0xFF);
        packet[off++] = seq;
        packet[off++] = (byte)(payLen & 0xFF);
        packet[off++] = (byte)((payLen >> 8) & 0xFF);
        if (payLen > 0)
        {
            Buffer.BlockCopy(payload, 0, packet, off, payLen);
        }
        return packet;
    }

    /// <summary>
    /// Encode a non-negative integer as 7-bit little-endian varint
    /// (OPPO OPOv1 LinkLen format: 低 7 位有效，bit7 表示是否继续）。
    /// </summary>
    public static byte[] EncodeVarint(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        if (value < 0x80) return new byte[] { (byte)value };

        var bytes = new List<byte>(4);
        int v = value;
        while (v >= 0x80)
        {
            bytes.Add((byte)((v & 0x7F) | 0x80));
            v >>= 7;
        }
        bytes.Add((byte)v);
        return bytes.ToArray();
    }

    /// <summary>
    /// Decode a 7-bit little-endian varint starting at <paramref name="offset"/>
    /// in <paramref name="data"/>. Returns (value, bytesConsumed).
    /// 长度上限 4 字节，足以表达 MaxFrameLen (512)。
    /// </summary>
    public static (int Value, int Bytes) DecodeVarint(byte[] data, int offset)
    {
        int value = 0;
        int shift = 0;
        int bytes = 0;
        for (int i = offset; i < data.Length && bytes < 4; i++, bytes++)
        {
            byte b = data[i];
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                bytes++;
                return (value, bytes);
            }
            shift += 7;
        }
        return (value, bytes);
    }

    /// <summary>
    /// 解析一帧的内部布局，兼容单字节与多字节 varint TotalLen。
    /// 内层布局：[Control/FSN 1B] [00 1B] [Cmd 2B LE] [Seq 1B] [PayLen 2B LE] [Payload...]
    /// </summary>
    /// <returns>true 表示成功解析；false 表示数据不完整或格式非法。</returns>
    public static bool TryGetPacketLayout(byte[] data, out PacketLayout layout)
    {
        layout = default;
        if (data.Length < 2 || data[0] != 0xAA) return false;

        var (totalLen, lenBytes) = DecodeVarint(data, 1);
        if (lenBytes == 0) return false;
        // 检查 varint 是否在合理字节数内终结（DecodeVarint 已限制 ≤4 字节）
        if (lenBytes > 0 && (data[lenBytes] & 0x80) != 0) return false;

        int frameLen = 1 + lenBytes + totalLen;
        if (data.Length < frameLen) return false;
        if (totalLen < 7) return false;

        int inner = 1 + lenBytes; // 跳过 AA + varint
        // inner: [Control/FSN] [00] [Cmd LE 2] [Seq 1] [PayLen LE 2] [Payload]
        int cmd = (data[inner + 2] & 0xFF) | ((data[inner + 3] & 0xFF) << 8);
        byte seq = data[inner + 4];
        int payLen = (data[inner + 5] & 0xFF) | ((data[inner + 6] & 0xFF) << 8);
        int payloadOffset = inner + 7;
        if (data.Length < payloadOffset + payLen) return false;

        layout = new PacketLayout(cmd, seq, payLen, payloadOffset, totalLen, lenBytes);
        return true;
    }
}

/// <summary>
/// 解析后的 OPPO 帧内部布局，兼容 varint TotalLen。
/// 所有 parser 应通过 <see cref="OppoPackets.TryGetPacketLayout"/> 获取此结构。
/// </summary>
public readonly record struct PacketLayout(
    int Cmd,
    byte Seq,
    int PayLen,
    int PayloadOffset,
    int TotalLen,
    int LenBytes);

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

        _pending.AddRange(buffer.AsSpan(0, length).ToArray());

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

            // TotalLen 是 7-bit LE varint，至少 1 字节，最多 4 字节
            int totalLen = 0;
            int lenBytes = 0;
            int shift = 0;
            bool complete = false;
            for (int i = 1; i < _pending.Count && i <= 4; i++)
            {
                byte b = _pending[i];
                totalLen |= (b & 0x7F) << shift;
                lenBytes = i;
                if ((b & 0x80) == 0)
                {
                    complete = true;
                    break;
                }
                shift += 7;
            }

            if (!complete)
            {
                // 缓冲区不足以读完 varint 长度字段，等待更多数据
                break;
            }

            int frameLen = totalLen + 1 + lenBytes; // AA + varint(totalLen) + payload
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
