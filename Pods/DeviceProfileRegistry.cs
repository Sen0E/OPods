namespace OPods.Pods;

/// <summary>
/// 机型配置注册表。维护已知机型 profile 列表，按蓝牙设备名解析出对应 profile，
/// 未匹配时返回 <see cref="GenericOppoProfile"/> 兜底。
/// </summary>
public static class DeviceProfileRegistry
{
    private static readonly DeviceProfile[] _profiles =
    {
        new EncoFree4Profile(),
        new EncoFree3Profile(),
    };

    private static readonly DeviceProfile _default = new GenericOppoProfile();

    /// <summary>默认兜底 profile（通用 OPPO 耳机）。</summary>
    public static DeviceProfile Default => _default;

    /// <summary>
    /// 按蓝牙设备名解析机型 profile。依次用各 profile 的 <see cref="DeviceProfile.NamePatterns"/>
    /// 做不区分大小写的子串匹配，命中第一个即返回；全部未命中返回 <see cref="Default"/>。
    /// </summary>
    public static DeviceProfile Resolve(string bluetoothName)
    {
        if (string.IsNullOrEmpty(bluetoothName)) return _default;
        foreach (var p in _profiles)
        {
            if (p.NamePatterns.Any(pat =>
                bluetoothName.Contains(pat, StringComparison.OrdinalIgnoreCase)))
            {
                return p;
            }
        }
        return _default;
    }
}
