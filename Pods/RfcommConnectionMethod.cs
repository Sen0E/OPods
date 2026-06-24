namespace OPods.Pods;

/// <summary>
/// RFCOMM connection method: SDP UUID lookup or fixed channel fallback.
/// </summary>
public enum RfcommConnectionMethod
{
    Uuid,
    Channel
}

/// <summary>Helpers for <see cref="RfcommConnectionMethod"/> preference persistence.</summary>
public static class RfcommConnectionMethodExtensions
{
    public const string PrefKey = "rfcomm_connection_method";

    private static readonly RfcommConnectionMethod[] Values =
        new[] { RfcommConnectionMethod.Uuid, RfcommConnectionMethod.Channel };

    public static string PreferenceValue(this RfcommConnectionMethod method) => method switch
    {
        RfcommConnectionMethod.Uuid => "uuid",
        RfcommConnectionMethod.Channel => "channel",
        _ => "uuid"
    };

    public static RfcommConnectionMethod FromPreference(string? value)
    {
        foreach (var v in Values)
        {
            if (v.PreferenceValue() == value) return v;
        }
        return RfcommConnectionMethod.Uuid;
    }

    public static RfcommConnectionMethod FromSelectedIndex(int index)
    {
        return (index >= 0 && index < Values.Length) ? Values[index] : RfcommConnectionMethod.Uuid;
    }

    public static int SelectedIndexOf(RfcommConnectionMethod method)
    {
        int idx = Array.IndexOf(Values, method);
        return idx >= 0 ? idx : 0;
    }
}
