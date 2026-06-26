namespace OPods.Pods;

/// <summary>
/// 通用兜底 profile，对应旧 Kotlin 参考的单一降噪模式。
/// 不主动匹配任何设备名，仅作为 <see cref="DeviceProfileRegistry.Resolve"/> 的默认返回值。
/// </summary>
public sealed class GenericOppoProfile : DeviceProfile
{
    /// <inheritdoc />
    public override string ModelName => "通用 OPPO 耳机";

    /// <inheritdoc />
    public override string[] NamePatterns => Array.Empty<string>();

    private static readonly AncModeDef[] _ancModes =
    {
        new(NoiseControlMode.Off,                   "关闭",   new byte[] { 0x01 },       (0x08, 0x00)),
        new(NoiseControlMode.NoiseCancellationDeep, "降噪",   new byte[] { 0x02 },       (0x10, 0x00)),
        new(NoiseControlMode.Adaptive,              "自适应", new byte[] { 0x00, 0x08 }, (0x00, 0x08)),
        new(NoiseControlMode.Transparency,          "通透",   new byte[] { 0x04 },       (0x04, 0x00)),
    };

    /// <inheritdoc />
    public override IReadOnlyList<AncModeDef> AncModes => _ancModes;
}
