using System.Drawing;

namespace SSOLauncher;

/// Central palette. Light mirrors the game's warm cream/pink look,
/// dark keeps the same accents on a deep plum ground.
internal static class ThemeColors
{
    public static Color Background { get; private set; }   // content ground
    public static Color Surface { get; private set; }      // sidebar
    public static Color Card { get; private set; }         // raised card face
    public static Color Ink { get; private set; }          // primary text
    public static Color Muted { get; private set; }        // secondary text
    public static Color Faint { get; private set; }        // hints, placeholders
    public static Color Primary { get; private set; }      // brand pink
    public static Color PrimaryHover { get; private set; }
    public static Color PrimaryPressed { get; private set; }
    public static Color OnPrimary { get; private set; }
    public static Color Highlight { get; private set; }    // selected nav pill
    public static Color HighlightSoft { get; private set; }// hovered nav pill
    public static Color Line { get; private set; }         // hairlines, input borders
    public static Color Gold { get; private set; }
    public static Color Success { get; private set; }
    public static Color Error { get; private set; }
    public static Color Shadow { get; private set; }
    public static int ShadowStrength { get; private set; }

    public static bool IsDark { get; private set; }

    static ThemeColors() => SetDarkMode(false);

    public static void SetDarkMode(bool dark)
    {
        IsDark = dark;
        if (dark)
        {
            Background = Color.FromArgb(0x17, 0x13, 0x20);
            Surface = Color.FromArgb(0x1F, 0x1A, 0x2B);
            Card = Color.FromArgb(0x27, 0x20, 0x36);
            Ink = Color.FromArgb(0xF3, 0xED, 0xF7);
            Muted = Color.FromArgb(0xA9, 0x9C, 0xB8);
            Faint = Color.FromArgb(0x7E, 0x72, 0x90);
            Primary = Color.FromArgb(0xF1, 0x3F, 0xAF);
            PrimaryHover = Color.FromArgb(0xFF, 0x63, 0xC2);
            PrimaryPressed = Color.FromArgb(0xC9, 0x18, 0x8C);
            OnPrimary = Color.White;
            Highlight = Color.FromArgb(0x34, 0x2B, 0x47);
            HighlightSoft = Color.FromArgb(0x2C, 0x24, 0x3B);
            Line = Color.FromArgb(0x3A, 0x31, 0x50);
            Gold = Color.FromArgb(0xE3, 0xB1, 0x55);
            Success = Color.FromArgb(0x4F, 0xB8, 0x73);
            Error = Color.FromArgb(0xF0, 0x73, 0x6B);
            Shadow = Color.Black;
            ShadowStrength = 10;
        }
        else
        {
            Background = Color.FromArgb(0xF7, 0xF1, 0xE4);
            Surface = Color.FromArgb(0xFF, 0xFC, 0xF6);
            Card = Color.White;
            Ink = Color.FromArgb(0x3B, 0x2C, 0x22);
            Muted = Color.FromArgb(0x8C, 0x7C, 0x6E);
            Faint = Color.FromArgb(0xB3, 0xA6, 0x97);
            Primary = Color.FromArgb(0xE5, 0x18, 0x9B);
            PrimaryHover = Color.FromArgb(0xF1, 0x3F, 0xAF);
            PrimaryPressed = Color.FromArgb(0xC2, 0x10, 0x7F);
            OnPrimary = Color.White;
            Highlight = Color.FromArgb(0xEF, 0xE4, 0xCE);
            HighlightSoft = Color.FromArgb(0xF5, 0xEE, 0xE0);
            Line = Color.FromArgb(0xE7, 0xDD, 0xCB);
            Gold = Color.FromArgb(0xD8, 0xA3, 0x3C);
            Success = Color.FromArgb(0x3E, 0x9E, 0x5C);
            Error = Color.FromArgb(0xD8, 0x44, 0x3C);
            Shadow = Color.FromArgb(0x2B, 0x1F, 0x14);
            ShadowStrength = 6;
        }
    }
}
