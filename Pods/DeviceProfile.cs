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
/// EQ 预设定义（机型专属，当前 C# 尚未实现 EQ 命令，仅预留数据）。
/// </summary>
public sealed record EqPresetDef(
    string DisplayName,
    byte PresetId);

/// <summary>
/// 机型配置抽象基类。所有机型相关数据（支持的 ANC 模式、连接参数、功能开关）
/// 收拢到此对象，运行时按蓝牙设备名解析出对应 profile，所有发包 / 解析 / UI 生成均查 profile。
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

    /// <summary>是否支持游戏模式。</summary>
    public virtual bool SupportsGameMode => true;

    /// <summary>默认的游戏模式实现方式。</summary>
    public virtual GameModeImplementation DefaultGameModeImpl => GameModeImplementation.Standard;

    /// <summary>是否支持空间音频。</summary>
    public virtual bool SupportsSpatialAudio => false;

    /// <summary>是否支持 EQ。</summary>
    public virtual bool SupportsEq => false;

    /// <summary>EQ 预设列表（仅当 <see cref="SupportsEq"/> 为 true 时有意义）。</summary>
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
}

/// <summary>默认 UUID 常量（OPPO/HeyMelody 通用）。</summary>
internal static class DefaultUuids
{
    public static readonly Guid Uuid1 = Guid.Parse("00001107-D102-11E1-9B23-00025B00A5A5");
    public static readonly Guid Uuid2 = Guid.Parse("0000079A-D102-11E1-9B23-00025B00A5A5");
}
