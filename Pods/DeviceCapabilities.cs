namespace OPods.Pods;

/// <summary>
/// 运行时设备能力对象。连接后通过协议查询动态填充，替代静态 Profile 的能力判定。
/// 字段缺失（null / false）表示对应查询未完成或设备不支持。
/// </summary>
public sealed class DeviceCapabilities
{
    /// <summary>0x810D 全量功能开关状态：featureId → 开关。null 表示尚未查询。</summary>
    public FeatureSwitchStatus? FeatureSwitches { get; internal set; }

    /// <summary>0x812A 空间音频类型码。null 表示未查询；0 通常表示关闭/不支持。</summary>
    public int? SpatialType { get; internal set; }

    /// <summary>0x8114 当前编解码器类型码。null 表示未查询。</summary>
    public int? CodecTypeCode { get; internal set; }

    /// <summary>0x8100 远端能力位掩码。null 表示未查询。</summary>
    public RemoteCapability? RemoteCapability { get; internal set; }

    /// <summary>0x8200 支持的通知 eventCode 列表。null 表示未查询。</summary>
    public NotificationCapability? NotificationCapability { get; internal set; }

    /// <summary>0x8105 固件版本（暂未实现解析，保留字段）。</summary>
    public string? FirmwareVersion { get; internal set; }

    /// <summary>能力发现是否已完成（至少 FeatureSwitches 已拿到）。</summary>
    public bool IsDiscovered => FeatureSwitches != null;

    // ---- 便捷能力判定 ----

    /// <summary>设备是否声明支持某 feature id（0x810D 响应中包含该项）。</summary>
    public bool IsFeatureSupported(int featureId) => FeatureSwitches?.Contains(featureId) ?? false;

    /// <summary>设备某 feature id 当前是否开启；不支持或未知返回 false。</summary>
    public bool IsFeatureEnabled(int featureId) => FeatureSwitches?.Get(featureId) ?? false;

    /// <summary>支持游戏模式主开关（feature 0x28）。</summary>
    public bool SupportsGameMode => IsFeatureSupported(FeatureId.GAME_SOUND_MAIN);

    /// <summary>支持低延迟游戏模式（feature 0x06）。</summary>
    public bool SupportsLowLatency => IsFeatureSupported(FeatureId.GAME_MODE);

    /// <summary>
    /// 支持空间音频：feature 0x1B 存在，或 0x812A spatialType 非 0。
    /// </summary>
    public bool SupportsSpatialAudio =>
        IsFeatureSupported(FeatureId.SPATIAL_TYPES) || (SpatialType is { } st && st != 0);

    /// <summary>空间音频当前开关（feature 0x1B）；不支持返回 false。</summary>
    public bool SpatialAudioEnabled => IsFeatureEnabled(FeatureId.SPATIAL_TYPES);

    /// <summary>支持 EQ：协议查询 0x010F 成功即视为支持。这里用 feature 表无直接位，
    /// 采用保守策略——EQ 查询响应到达即支持。运行时由 PodController 在收到 0x810F 时置位。</summary>
    public bool SupportsEq { get; internal set; }

    /// <summary>支持佩戴检测（feature 0x04）。</summary>
    public bool SupportsWearDetection => IsFeatureSupported(FeatureId.WEAR_DETECTION);

    /// <summary>支持多设备连接（feature 0x11）。</summary>
    public bool SupportsMultiDevice => IsFeatureSupported(FeatureId.MULTI_DEVICES_CONNECT);

    /// <summary>支持听感增强（feature 0x0B）。</summary>
    public bool SupportsHearingEnhancement => IsFeatureSupported(FeatureId.HEARING_ENHANCEMENT);

    /// <summary>编解码器友好名；未知返回 null。</summary>
    public string? CodecName => CodecTypeCode is { } t ? CodecType.ToName(t) : null;

    /// <summary>当前佩戴状态；未查询或未上报为 null。</summary>
    public WearStatus? WearStatus { get; internal set; }
}
