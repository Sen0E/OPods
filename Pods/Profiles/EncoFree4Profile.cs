namespace OPods.Pods;

/// <summary>
/// OPPO Enco Free4 机型配置。
/// 基于 Python 参考实现 (oppopods_ctk.py) 的 ANC_MAP / EQ_PRESETS。
/// 支持 4 级降噪（智能/轻度/中度/深度）、自适应、通透，以及空间音频与 EQ。
/// </summary>
public sealed class EncoFree4Profile : DeviceProfile
{
    private IReadOnlyDictionary<(int, int), NoiseControlMode>? _ancResponseMap;

    /// <inheritdoc />
    public override string ModelName => "OPPO Enco Free4";

    /// <inheritdoc />
    public override string[] NamePatterns => new[] { "Enco Free4", "Enco Free 4" };

    private static readonly AncModeDef[] _ancModes =
    {
        new(NoiseControlMode.Off,                     "关闭",   new byte[] { 0x01 },       (0x08, 0x00)),
        new(NoiseControlMode.NoiseCancellationSmart,  "智能",   new byte[] { 0x80 },       (0x80, 0x00)),
        new(NoiseControlMode.NoiseCancellationLight,  "轻度",   new byte[] { 0x40 },       (0x40, 0x00)),
        new(NoiseControlMode.NoiseCancellationMedium, "中度",   new byte[] { 0x20 },       (0x20, 0x00)),
        new(NoiseControlMode.NoiseCancellationDeep,   "深度",   new byte[] { 0x10 },       (0x10, 0x00)),
        new(NoiseControlMode.Adaptive,                "自适应", new byte[] { 0x00, 0x08 }, (0x00, 0x08)),
        new(NoiseControlMode.Transparency,            "通透",   new byte[] { 0x04 },       (0x04, 0x00)),
    };

    /// <inheritdoc />
    public override IReadOnlyList<AncModeDef> AncModes => _ancModes;

    /// <inheritdoc />
    public override bool SupportsSpatialAudio => true;

    /// <inheritdoc />
    public override bool SupportsEq => true;

    private static readonly EqPresetDef[] _eqPresets =
    {
        new("至臻原音", 0),
        new("纯享人声", 1),
        new("澎湃低音", 2),
        new("活力动感", 7),
    };

    /// <inheritdoc />
    public override IReadOnlyList<EqPresetDef> EqPresets => _eqPresets;

    /// <summary>
    /// 响应码映射。在基础一对一映射之上补充别名：
    /// (0x02,0x00) → 智能； (0x00,0x01)/(0x00,0x02) → 通透。
    /// </summary>
    public override IReadOnlyDictionary<(int, int), NoiseControlMode> AncResponseMap
    {
        get
        {
            if (_ancResponseMap is null)
            {
                var map = new Dictionary<(int, int), NoiseControlMode>(base.AncResponseMap)
                {
                    [(0x02, 0x00)] = NoiseControlMode.NoiseCancellationSmart,
                    [(0x00, 0x01)] = NoiseControlMode.Transparency,
                    [(0x00, 0x02)] = NoiseControlMode.Transparency,
                };
                _ancResponseMap = map;
            }
            return _ancResponseMap;
        }
    }
}
