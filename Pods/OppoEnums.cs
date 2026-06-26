namespace OPods.Pods;

/// <summary>Pre-built OPPO protocol packets (1:1 with Kotlin <c>Enums</c>).</summary>
public static class OppoEnums
{
    /// <summary>Query battery: AA 07 00 00 06 01 F0 00 00</summary>
    public static readonly byte[] QueryBattery =
    {
        0xAA, 0x07, 0x00, 0x00, 0x06, 0x01, 0xF0, 0x00, 0x00
    };

    /// <summary>Query ANC mode: AA 09 00 00 0C 01 00 02 00 01 01</summary>
    public static readonly byte[] QueryAnc = OppoPackets.BuildPacket(
        cmd: Cmd.QUERY_ANC_MODE, payload: new byte[] { 0x01, 0x01 });

    /// <summary>Query EQ preset: AA 07 00 00 0F 01 F0 00 00</summary>
    public static readonly byte[] QueryEq =
        OppoPackets.BuildPacket(cmd: Cmd.QUERY_EQ);

    /// <summary>Enable game mode main switch (feature 0x28): AA 09 00 00 03 04 00 02 00 28 01</summary>
    public static readonly byte[] GameModeOn = OppoPackets.BuildPacket(
        cmd: Cmd.SET_GAME_MODE, payload: new byte[] { (byte)FeatureId.GAME_SOUND_MAIN, 0x01 });

    /// <summary>Disable game mode main switch (feature 0x28): AA 09 00 00 03 04 00 02 00 28 00</summary>
    public static readonly byte[] GameModeOff = OppoPackets.BuildPacket(
        cmd: Cmd.SET_GAME_MODE, payload: new byte[] { (byte)FeatureId.GAME_SOUND_MAIN, 0x00 });

    /// <summary>Enable low-latency game mode (feature 0x06): AA 09 00 00 03 04 00 02 00 06 01</summary>
    public static readonly byte[] GameLowLatencyOn = OppoPackets.BuildPacket(
        cmd: Cmd.SET_GAME_MODE, payload: new byte[] { (byte)FeatureId.GAME_MODE, 0x01 });

    /// <summary>Disable low-latency game mode (feature 0x06): AA 09 00 00 03 04 00 02 00 06 00</summary>
    public static readonly byte[] GameLowLatencyOff = OppoPackets.BuildPacket(
        cmd: Cmd.SET_GAME_MODE, payload: new byte[] { (byte)FeatureId.GAME_MODE, 0x00 });

    // ---- 协议优先重构新增：能力发现查询包 ----

    /// <summary>Query remote capability (0x0100).</summary>
    public static readonly byte[] QueryCapability =
        OppoPackets.BuildPacket(cmd: Cmd.QUERY_CAPABILITY);

    /// <summary>Query MTU (0x0101). Payload 00 02 = 512 (LE).</summary>
    public static readonly byte[] QueryMtu =
        OppoPackets.BuildPacket(cmd: Cmd.QUERY_MTU, payload: new byte[] { 0x00, 0x02 });

    /// <summary>Query VID (0x0102). Payload 00 00 = local VID 0 (LE).</summary>
    public static readonly byte[] QueryVid =
        OppoPackets.BuildPacket(cmd: Cmd.QUERY_VID, payload: new byte[] { 0x00, 0x00 });

    /// <summary>Query PID (0x0103).</summary>
    public static readonly byte[] QueryPid =
        OppoPackets.BuildPacket(cmd: Cmd.QUERY_PID);

    /// <summary>Query current codec type (0x0114).</summary>
    public static readonly byte[] QueryCodec =
        OppoPackets.BuildPacket(cmd: Cmd.QUERY_CODEC);

    /// <summary>Query headset spatial type (0x012A).</summary>
    public static readonly byte[] QuerySpatialType =
        OppoPackets.BuildPacket(cmd: Cmd.QUERY_SPATIAL_TYPE);

    /// <summary>Query notification capabilities (0x0200).</summary>
    public static readonly byte[] QueryNotifCapability =
        OppoPackets.BuildPacket(cmd: Cmd.NOTIF_CAPABILITY);

    // ---- 空间音频开关（0x0403 + feature 0x1B）----

    /// <summary>Enable spatial audio (0x0403 payload 1B 01).</summary>
    public static readonly byte[] SpatialOn = OppoPackets.BuildPacket(
        cmd: Cmd.SET_GAME_MODE, payload: new byte[] { (byte)FeatureId.SPATIAL_TYPES, 0x01 });

    /// <summary>Disable spatial audio (0x0403 payload 1B 00).</summary>
    public static readonly byte[] SpatialOff = OppoPackets.BuildPacket(
        cmd: Cmd.SET_GAME_MODE, payload: new byte[] { (byte)FeatureId.SPATIAL_TYPES, 0x00 });

    // ---- 通知注册 ----

    /// <summary>Build a single-event notification register packet (0x0201).</summary>
    public static byte[] BuildNotifRegister(int eventCode) =>
        OppoPackets.BuildPacket(cmd: Cmd.NOTIF_REGISTER, payload: new byte[] { (byte)eventCode });

    /// <summary>Build a multi-event notification register packet (0x0205).</summary>
    public static byte[] BuildNotifRegisterMulti(int[] eventCodes)
    {
        var payload = new byte[1 + eventCodes.Length];
        payload[0] = (byte)eventCodes.Length;
        for (int i = 0; i < eventCodes.Length; i++) payload[1 + i] = (byte)eventCodes[i];
        return OppoPackets.BuildPacket(cmd: Cmd.NOTIF_REGISTER_MULTI, payload: payload);
    }

    /// <summary>
    /// 动态构造功能开关批量查询包（0x010D）。
    /// 载荷格式：&lt;count:1&gt; &lt;featureId:1&gt;...（首字节是数量，不是 feature id）。
    /// 替代旧的写死 <c>QueryStatus</c> blob，按运行时能力动态拼 feature id 列表。
    /// </summary>
    public static byte[] BuildFeatureSwitchQuery(int[] featureIds)
    {
        var payload = new byte[1 + featureIds.Length];
        payload[0] = (byte)featureIds.Length;
        for (int i = 0; i < featureIds.Length; i++) payload[1 + i] = (byte)featureIds[i];
        return OppoPackets.BuildPacket(cmd: Cmd.QUERY_STATUS, payload: payload);
    }

    /// <summary>
    /// 全量功能开关查询包，使用 <see cref="FeatureId.All"/> 列表。
    /// 用于连接后的能力发现阶段。
    /// </summary>
    public static byte[] QueryFeatureSwitchAll => BuildFeatureSwitchQuery(FeatureId.All);

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
