namespace OPods.Pods;

/// <summary>
/// Game-mode implementation strategy.
/// STANDARD only toggles the main switch; COMPATIBLE also toggles low-latency
/// with a 120ms inter-packet delay (matches the Kotlin reference).
/// </summary>
public enum GameModeImplementation
{
    Standard,
    Compatible
}

/// <summary>Helpers for <see cref="GameModeImplementation"/> preference persistence.</summary>
public static class GameModeImplementationExtensions
{
    public const string PrefKey = "game_mode_implementation";

    private static readonly GameModeImplementation[] Values =
        new[] { GameModeImplementation.Standard, GameModeImplementation.Compatible };

    public static string PreferenceValue(this GameModeImplementation impl) => impl switch
    {
        GameModeImplementation.Standard => "standard",
        GameModeImplementation.Compatible => "compatible",
        _ => "standard"
    };

    public static GameModeImplementation FromPreference(string? value)
    {
        foreach (var v in Values)
        {
            if (v.PreferenceValue() == value) return v;
        }
        return GameModeImplementation.Standard;
    }

    public static GameModeImplementation FromSelectedIndex(int index)
    {
        return (index >= 0 && index < Values.Length) ? Values[index] : GameModeImplementation.Standard;
    }

    public static int SelectedIndexOf(GameModeImplementation implementation)
    {
        int idx = Array.IndexOf(Values, implementation);
        return idx >= 0 ? idx : 0;
    }
}
