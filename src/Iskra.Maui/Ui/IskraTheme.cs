namespace Iskra.Maui;

internal static class IskraTheme
{
    public static ThemePalette Current { get; private set; } = ThemeCatalog.Get(ThemeKind.DarkFlame);

    public static Color Accent => Current.Accent;
    public static Color Online => Current.Online;
    public static Color Offline => Current.Offline;
    public static Color Text => Current.TextPrimary;
    public static Color Muted => Current.Muted;
    public static Color SentText => Current.SentText;
    public static Color IncomingBubble => Current.IncomingBubble;
    public static Color OutgoingBubble => Current.OutgoingBubble;
    public static Color Check => Current.Check;
    public static Color Danger => Current.Danger;

    internal static void Set(ThemePalette palette)
    {
        Current = palette;
    }

    private static readonly Color[] AvatarColors =
    [
        Color.FromArgb("#5B8DEF"),
        Color.FromArgb("#E39B2B"),
        Color.FromArgb("#34C759"),
        Color.FromArgb("#AF52DE"),
        Color.FromArgb("#FF6B6B"),
        Color.FromArgb("#32ADE6"),
        Color.FromArgb("#FF9500"),
        Color.FromArgb("#5856D6")
    ];

    public static Color AvatarColor(string key)
    {
        if (string.IsNullOrEmpty(key))
            return AvatarColors[0];
        var hash = 0;
        foreach (var c in key)
            hash = (hash * 31) + c;
        return AvatarColors[Math.Abs(hash) % AvatarColors.Length];
    }

    public static string Initials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        var s = parts[0];
        return s.Length >= 2
            ? $"{char.ToUpperInvariant(s[0])}{char.ToUpperInvariant(s[1])}"
            : char.ToUpperInvariant(s[0]).ToString();
    }
}
