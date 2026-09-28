using Hades.SaveFormat;

namespace Hades1Editor;

public static class Hades1Fields
{
    public static readonly SaveField[] All =
    [
        new("darkness", "Darkness held", ["GameState", "Resources", "MetaPoints"]),
        new("keys", "Chthonic Keys held", ["GameState", "Resources", "LockKeys"]),
        new("godmode-level", "God Mode level", ["GameState", "EasyModeLevel"], Max: 30),
    ];
}
