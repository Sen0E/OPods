namespace OPods.Pods;

/// <summary>
/// Noise control mode enum for UI / controller state.
/// 此枚举是所有已知 OPPO 机型的 ANC 模式超集，单个机型通过 <see cref="DeviceProfile"/> 声明自己支持的子集。
/// </summary>
public enum NoiseControlMode
{
    /// <summary>关闭降噪。</summary>
    Off,

    /// <summary>智能/动态降噪。</summary>
    NoiseCancellationSmart,

    /// <summary>轻度降噪。</summary>
    NoiseCancellationLight,

    /// <summary>中度降噪。</summary>
    NoiseCancellationMedium,

    /// <summary>深度降噪。</summary>
    NoiseCancellationDeep,

    /// <summary>自适应降噪。</summary>
    Adaptive,

    /// <summary>通透模式。</summary>
    Transparency
}
