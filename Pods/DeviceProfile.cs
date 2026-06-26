namespace OPods.Pods;

/// <summary>
/// 描述单个 ANC 模式的完整定义：UI 显示名、SET_ANC 载荷字节、响应识别码。
/// </summary>
public sealed record AncModeDef(
    NoiseControlMode Mode,
    string DisplayName,
    byte[] SetPayload,
    (int V1, int V2) ResponseCode);

/// <summary>
/// EQ 预设定义：UI 显示名 + 协议 preset id（SET_EQ 载荷单字节）。
/// </summary>
public sealed record EqPresetDef(
    string DisplayName,
    byte PresetId);

/// <summary>
/// 机型配置抽象基类。协议优先重构后，profile 仅负责「协议未直接暴露的兜底提示」：
/// ANC 模式枚举与显示名/响应码映射、连接参数、EQ 预设显示名。
/// 空间音频/EQ/游戏模式等功能开关的可见性改由 <see cref="DeviceCapabilities"/>（运行时协议发现）决定。
/// </summary>
public abstract class DeviceProfile
{
    private IReadOnlyDictionary<(int, int), NoiseControlMode>? _ancResponseMap;

    /// <summary>机型显示名，如 "OPPO Enco Free4"。</summary>
    public abstract string ModelName { get; }

    /// <summary>蓝牙设备名匹配规则（不区分大小写的子串匹配）。</summary>
    public abstract string[] NamePatterns { get; }

    /// <summary>该机型支持的 ANC 模式列表。</summary>
    public abstract IReadOnlyList<AncModeDef> AncModes { get; }

    /// <summary>连接用的 UUID 列表（默认使用 <see cref="DefaultUuids.Uuid1"/> / <see cref="DefaultUuids.Uuid2"/>）。</summary>
    public virtual Guid[] Uuids => new[] { DefaultUuids.Uuid1, DefaultUuids.Uuid2 };

    /// <summary>固定 RFCOMM 通道号，null 表示仅使用 UUID 模式。</summary>
    public virtual int? RfcommChannel => 15;

    /// <summary>推荐的连接方式（可被用户选择覆盖）。</summary>
    public virtual RfcommConnectionMethod PreferredMethod => RfcommConnectionMethod.Uuid;

    /// <summary>
    /// EQ 预设显示名列表（仅作 UI 显示名映射；是否支持 EQ 由
    /// <see cref="DeviceCapabilities.SupportsEq"/> 决定）。空列表表示无预设名，
    /// UI 将回退到原始 preset id 输入。
    /// </summary>
    public virtual IReadOnlyList<EqPresetDef> EqPresets => Array.Empty<EqPresetDef>();

    /// <summary>
    /// 响应码 (v1,v2) → 模式 的映射表，用于解析 ANC 响应包。
    /// 默认实现由 <see cref="AncModes"/> 一对一生成；机型可重写以补充别名。
    /// </summary>
    public virtual IReadOnlyDictionary<(int, int), NoiseControlMode> AncResponseMap
    {
        get
        {
            if (_ancResponseMap is null)
            {
                _ancResponseMap = AncModes.ToDictionary(m => m.ResponseCode, m => m.Mode);
            }
            return _ancResponseMap;
        }
    }

    /// <summary>
    /// 为指定模式构造 SET_ANC 命令包。
    /// 载荷格式为 [0x01, 0x01, ...SetPayload]。
    /// </summary>
    /// <exception cref="ArgumentException">该 profile 不支持指定的 <paramref name="mode"/>。</exception>
    public byte[] BuildAncPacket(NoiseControlMode mode)
    {
        var def = AncModes.FirstOrDefault(m => m.Mode == mode)
            ?? throw new ArgumentException($"Profile {ModelName} 不支持 ANC 模式 {mode}");
        var payload = new byte[2 + def.SetPayload.Length];
        payload[0] = 0x01;
        payload[1] = 0x01;
        Buffer.BlockCopy(def.SetPayload, 0, payload, 2, def.SetPayload.Length);
        return OppoPackets.BuildPacket(Cmd.SET_ANC, payload: payload);
    }

    /// <summary>
    /// 构造 SET_EQ 命令包。载荷为单字节 preset id。
    /// 是否允许调用由 <see cref="DeviceCapabilities.SupportsEq"/> 决定，本方法不再做 profile 级守卫。
    /// </summary>
    public byte[] BuildEqPacket(byte presetId) =>
        OppoPackets.BuildPacket(Cmd.SET_EQ, payload: new byte[] { presetId });

    /// <summary>
    /// 按 preset id 反查 EQ 预设；未命中返回 null。
    /// </summary>
    public EqPresetDef? ResolveEqPreset(byte presetId) =>
        EqPresets.FirstOrDefault(p => p.PresetId == presetId);
}

/// <summary>默认 UUID 常量（OPPO/HeyMelody 通用）。</summary>
internal static class DefaultUuids
{
    public static readonly Guid Uuid1 = Guid.Parse("00001107-D102-11E1-9B23-00025B00A5A5");
    public static readonly Guid Uuid2 = Guid.Parse("0000079A-D102-11E1-9B23-00025B00A5A5");
}
