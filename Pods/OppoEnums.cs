namespace OPods.Pods;

/// <summary>Pre-built OPPO protocol packets (1:1 with Kotlin <c>Enums</c>).</summary>
public static class OppoEnums
{
    /// <summary>Switch to Noise Cancellation: AA 0A 00 00 04 04 00 03 00 01 01 02</summary>
    public static readonly byte[] AncNoiseCancel = OppoPackets.BuildPacket(
        cmd: Cmd.SET_ANC, payload: new byte[] { 0x01, 0x01, (byte)AncMode.NOISE_CANCELLATION });

    /// <summary>Switch to Transparency: AA 0A 00 00 04 04 00 03 00 01 01 04</summary>
    public static readonly byte[] AncTransparency = OppoPackets.BuildPacket(
        cmd: Cmd.SET_ANC, payload: new byte[] { 0x01, 0x01, (byte)AncMode.TRANSPARENCY });

    /// <summary>Switch to Off: AA 0A 00 00 04 04 00 03 00 01 01 01</summary>
    public static readonly byte[] AncOff = OppoPackets.BuildPacket(
        cmd: Cmd.SET_ANC, payload: new byte[] { 0x01, 0x01, (byte)AncMode.OFF });

    /// <summary>Switch to Adaptive: AA 0B 00 00 04 04 00 04 00 01 01 00 08</summary>
    public static readonly byte[] AncAdaptive = OppoPackets.BuildPacket(
        cmd: Cmd.SET_ANC, payload: new byte[] { 0x01, 0x01, (byte)AncMode.ADAPTIVE_HIGH, (byte)AncMode.ADAPTIVE_LOW });

    /// <summary>Query battery: AA 07 00 00 06 01 F0 00 00</summary>
    public static readonly byte[] QueryBattery =
    {
        0xAA, 0x07, 0x00, 0x00, 0x06, 0x01, 0xF0, 0x00, 0x00
    };

    /// <summary>Query ANC mode: AA 09 00 00 0C 01 00 02 00 01 01</summary>
    public static readonly byte[] QueryAnc = OppoPackets.BuildPacket(
        cmd: Cmd.QUERY_ANC_MODE, payload: new byte[] { 0x01, 0x01 });

    /// <summary>Enable game mode main switch: AA 09 00 00 03 04 00 02 00 28 01</summary>
    public static readonly byte[] GameModeOn = OppoPackets.BuildPacket(
        cmd: Cmd.SET_GAME_MODE, payload: new byte[] { (byte)GameModeFeature.MAIN, 0x01 });

    /// <summary>Disable game mode main switch: AA 09 00 00 03 04 00 02 00 28 00</summary>
    public static readonly byte[] GameModeOff = OppoPackets.BuildPacket(
        cmd: Cmd.SET_GAME_MODE, payload: new byte[] { (byte)GameModeFeature.MAIN, 0x00 });

    /// <summary>Enable low-latency game mode: AA 09 00 00 03 04 00 02 00 06 01</summary>
    public static readonly byte[] GameLowLatencyOn = OppoPackets.BuildPacket(
        cmd: Cmd.SET_GAME_MODE, payload: new byte[] { (byte)GameModeFeature.LOW_LATENCY, 0x01 });

    /// <summary>Disable low-latency game mode: AA 09 00 00 03 04 00 02 00 06 00</summary>
    public static readonly byte[] GameLowLatencyOff = OppoPackets.BuildPacket(
        cmd: Cmd.SET_GAME_MODE, payload: new byte[] { (byte)GameModeFeature.LOW_LATENCY, 0x00 });

    /// <summary>
    /// Batch parameter query (fixed hex blob). Cmd=0x010D, contains multiple
    /// param IDs including 0x28 (game mode). Has built-in wake weight.
    /// </summary>
    public static readonly byte[] QueryStatus =
    {
        0xAA, 0x13, 0x00, 0x00, 0x0D, 0x01, 0x00, 0x0C, 0x00,
        0x0B, 0x05, 0x04, 0x0B, 0x11, 0x13, 0x18, 0x06, 0x1B, 0x1C, 0x27, 0x28
    };

    /// <summary>
    /// Build the ordered packet sequence to toggle game mode for the given implementation.
    /// COMPATIBLE mode inserts a 120ms delay between packets (handled by caller).
    /// </summary>
    public static List<byte[]> GameModePackets(bool enabled, GameModeImplementation implementation)
    {
        return implementation switch
        {
            GameModeImplementation.Standard => new List<byte[]>
            {
                enabled ? GameModeOn : GameModeOff
            },
            GameModeImplementation.Compatible => enabled
                ? new List<byte[]> { GameModeOn, GameLowLatencyOn }
                : new List<byte[]> { GameLowLatencyOff, GameModeOff },
            _ => new List<byte[]> { enabled ? GameModeOn : GameModeOff }
        };
    }
}
