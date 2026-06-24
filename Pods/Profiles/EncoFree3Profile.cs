namespace OPods.Pods;

/// <summary>
/// OPPO Enco Free3 机型配置。
/// 支持 4 级降噪（智能/轻度/中度/深度）、通透，以及空间音频与 EQ。
/// </summary>
public sealed class EncoFree3Profile : DeviceProfile
{
    /// <inheritdoc />
    public override string ModelName => "OPPO Enco Free3";

    /// <inheritdoc />
    public override string[] NamePatterns => new[] { "Enco Free3", "Enco Free 3" };

    private static readonly AncModeDef[] _ancModes =
    {
        new(NoiseControlMode.Off,                     "关闭",   new byte[] { 0x01 },       (0x08, 0x00)),
        new(NoiseControlMode.NoiseCancellationSmart,  "智能",   new byte[] { 0x80 },       (0x80, 0x00)),
        new(NoiseControlMode.NoiseCancellationLight,  "轻度",   new byte[] { 0x40 },       (0x40, 0x00)),
        new(NoiseControlMode.NoiseCancellationMedium, "中度",   new byte[] { 0x20 },       (0x20, 0x00)),
        new(NoiseControlMode.NoiseCancellationDeep,   "深度",   new byte[] { 0x10 },       (0x10, 0x00)),
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
}

