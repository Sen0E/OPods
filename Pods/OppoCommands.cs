namespace OPods.Pods;

/// <summary>Protocol command codes (little-endian on the wire).</summary>
public static class Cmd
{
    public const int SET_ANC = 0x0404;
    public const int SET_GAME_MODE = 0x0403;
    public const int SET_EQ = 0x0406;

    public const int QUERY_BATTERY = 0x0106;
    public const int BATTERY_RESPONSE = 0x8106;

    public const int QUERY_ANC_MODE = 0x010C;
    public const int ANC_MODE_RESPONSE = 0x810C;
    public const int ANC_MODE_NOTIFY = 0x0204;

    public const int QUERY_STATUS = 0x010D;
    public const int QUERY_STATUS_RESPONSE = 0x810D;

    public const int SET_GAME_MODE_RESPONSE = 0x8403;

    public const int QUERY_EQ = 0x010F;
    public const int EQ_RESPONSE = 0x810F;

    // 协议优先重构新增：查询/响应命令（0x0100 段）
    public const int QUERY_CAPABILITY = 0x0100;
    public const int CAPABILITY_RESPONSE = 0x8100;

    public const int QUERY_MTU = 0x0101;
    public const int MTU_RESPONSE = 0x8101;

    public const int QUERY_VID = 0x0102;
    public const int VID_RESPONSE = 0x8102;

    public const int QUERY_PID = 0x0103;
    public const int PID_RESPONSE = 0x8103;

    public const int QUERY_CODEC = 0x0114;
    public const int CODEC_RESPONSE = 0x8114;

    public const int QUERY_SPATIAL_TYPE = 0x012A;
    public const int SPATIAL_TYPE_RESPONSE = 0x812A;

    // 通知机制（0x0200 段）
    public const int NOTIF_CAPABILITY = 0x0200;
    public const int NOTIF_CAPABILITY_RESPONSE = 0x8200;

    public const int NOTIF_REGISTER = 0x0201;
    public const int NOTIF_REGISTER_RESPONSE = 0x8201;

    public const int NOTIF_REGISTER_MULTI = 0x0205;
    public const int NOTIF_REGISTER_MULTI_RESPONSE = 0x8205;

    public const int NOTIF_EVENT = 0x0204;
}

/// <summary>Battery component index in response payload.</summary>
public static class BatteryComponent
{
    public const int LEFT = 1;
    public const int RIGHT = 2;
    public const int CASE = 3;
}

/// <summary>
/// Feature IDs used by <c>0x010D</c> batch query / <c>0x810D</c> response and
/// <c>0x0403</c> set-feature-switch commands. 来源：HeyMelody 官方 App 反编译。
/// </summary>
public static class FeatureId
{
    /// <summary>佩戴检测 wearDetection。</summary>
    public const int WEAR_DETECTION = 0x04;

    /// <summary>游戏模式（低延迟）gameMode。</summary>
    public const int GAME_MODE = 0x06;

    /// <summary>人声增强 vocalEnhance。</summary>
    public const int VOCAL_ENHANCE = 0x09;

    /// <summary>听感增强 hearingEnhancement。</summary>
    public const int HEARING_ENHANCEMENT = 0x0B;

    /// <summary>个性化降噪 personalNoise。</summary>
    public const int PERSONAL_NOISE = 0x0C;

    /// <summary>禅模式 zenMode。</summary>
    public const int ZEN_MODE = 0x0F;

    /// <summary>多设备连接 multiDevicesConnect。</summary>
    public const int MULTI_DEVICES_CONNECT = 0x11;

    /// <summary>录音 headSetSoundRecord。</summary>
    public const int HEADSET_SOUND_RECORD = 0x13;

    /// <summary>语音唤醒 voiceWake。</summary>
    public const int VOICE_WAKE = 0x14;

    /// <summary>智能通话 smartCall。</summary>
    public const int SMART_CALL = 0x15;

    /// <summary>设备丢失提醒 deviceLostRemind。</summary>
    public const int DEVICE_LOST_REMIND = 0x16;

    /// <summary>长续航 longPowerMode。</summary>
    public const int LONG_POWER_MODE = 0x17;

    /// <summary>高音质 highToneQuality。</summary>
    public const int HIGH_TONE_QUALITY = 0x18;

    /// <summary>语音指令 voiceCommand。</summary>
    public const int VOICE_COMMAND = 0x19;

    /// <summary>空间音频类型 spatialTypes。</summary>
    public const int SPATIAL_TYPES = 0x1B;

    /// <summary>自动音量 controlAutoVolume。</summary>
    public const int CONTROL_AUTO_VOLUME = 0x1C;

    /// <summary>低音引擎 bassEngine。</summary>
    public const int BASS_ENGINE = 0x1D;

    /// <summary>收集日志 collectLogs。</summary>
    public const int COLLECT_LOGS = 0x1E;

    /// <summary>游戏 EQ 包 gameEqPkgList。</summary>
    public const int GAME_EQ_PKG_LIST = 0x21;

    /// <summary>游戏音效列表 gameSoundList（变体 1）。</summary>
    public const int GAME_SOUND_LIST = 0x27;

    /// <summary>游戏音效列表主开关 / 游戏模式主开关 gameSoundList（变体 2）。</summary>
    public const int GAME_SOUND_MAIN = 0x28;

    /// <summary>自适应音量 adaptiveVolume。</summary>
    public const int ADAPTIVE_VOLUME = 0x30;

    /// <summary>自适应耳道 adaptiveEar。</summary>
    public const int ADAPTIVE_EAR = 0x31;

    /// <summary>会议助手 meetingAssistant。</summary>
    public const int MEETING_ASSISTANT = 0x34;

    /// <summary>长按音量 longPressVolume。</summary>
    public const int LONG_PRESS_VOLUME = 0x35;

    /// <summary>swiftPair。</summary>
    public const int SWIFT_PAIR = 0x37;

    /// <summary>听力优化 hearingOptimize。</summary>
    public const int HEARING_OPTIMIZE = 0x38;

    /// <summary>来电控制 incomingCallControl。</summary>
    public const int INCOMING_CALL_CONTROL = 0x39;

    /// <summary>
    /// 全量 feature id 列表，用于能力发现阶段构造
    /// <see cref="OppoEnums.BuildFeatureSwitchQuery"/> 查询包。
    /// 来源：HeyMelody_Official_App_Protocol_Findings.md 第 4.3 节。
    /// </summary>
    public static readonly int[] All =
    {
        WEAR_DETECTION,
        GAME_MODE,
        VOCAL_ENHANCE,
        HEARING_ENHANCEMENT,
        PERSONAL_NOISE,
        ZEN_MODE,
        MULTI_DEVICES_CONNECT,
        HEADSET_SOUND_RECORD,
        VOICE_WAKE,
        SMART_CALL,
        DEVICE_LOST_REMIND,
        LONG_POWER_MODE,
        HIGH_TONE_QUALITY,
        VOICE_COMMAND,
        SPATIAL_TYPES,
        CONTROL_AUTO_VOLUME,
        BASS_ENGINE,
        COLLECT_LOGS,
        GAME_EQ_PKG_LIST,
        GAME_SOUND_LIST,
        GAME_SOUND_MAIN,
        ADAPTIVE_VOLUME,
        ADAPTIVE_EAR,
        MEETING_ASSISTANT,
        LONG_PRESS_VOLUME,
        SWIFT_PAIR,
        HEARING_OPTIMIZE,
        INCOMING_CALL_CONTROL,
    };
}
