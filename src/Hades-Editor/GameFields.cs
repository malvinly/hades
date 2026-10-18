using Hades.SaveFormat;

namespace HadesEditor;

public static class GameFields
{
    private static readonly SaveField GodModeLevel = new("godmode-level", "God Mode level", ["GameState", "EasyModeLevel"], Max: 30);
    private static readonly SaveField Nectar = new("nectar", "Nectar held", ["GameState", "Resources", "GiftPoints"]);

    public static readonly Dictionary<Game, SaveField[]> ByGame = new()
    {
        [Game.Hades1] =
        [
            new("darkness", "Darkness held", ["GameState", "Resources", "MetaPoints"]),
            new("keys", "Chthonic Keys held", ["GameState", "Resources", "LockKeys"]),
            Nectar,
            GodModeLevel,
        ],
        [Game.Hades2] =
        [
            new("bones", "Bones held", ["GameState", "Resources", "MetaCurrency"]),
            new("ashes", "Ashes held", ["GameState", "Resources", "MetaCardPointsCommon"]),
            new("psyche", "Psyche held", ["GameState", "Resources", "MemPointsCommon"]),
            Nectar,
            GodModeLevel,
        ],
    };

    public static string Label(Game game) => game == Game.Hades1 ? "Hades 1" : "Hades 2";
}
