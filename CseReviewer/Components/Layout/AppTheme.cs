using MudBlazor;

namespace CseReviewer.Components.Layout;
public static class AppTheme {
    #region Fonts
    public const string HeadingFont = "Newsreader";
    public const string BodyFont = "Public Sans";
    #endregion

    #region Colors
    public const string InkLight = "#1c1b19";
    public const string MutedLight = "#5d5a53";
    public const string PaperLight = "#f7f4ec";
    public const string SurfaceLight = "#fffdf8";
    public const string RuleLight = "#d9d3c4";
    public const string AccentLight = "#1f3a5f";
    public const string InkDark = "#ece7dc";
    public const string MutedDark = "#a6a195";
    public const string PaperDark = "#151618";
    public const string SurfaceDark = "#1d1e21";
    public const string RuleDark = "#34353a";
    public const string AccentDark = "#a9c1e6";
    #endregion

    #region Layout
    public const MaxWidth PageMaxWidth = MaxWidth.Large;
    public const string PageSpacing = "my-8";
    public const string Panel = "panel pa-6";
    public const string LargePanel = "panel pa-6 pa-md-8";
    public const string SectionGap = "mt-8";
    public const string BorderRadius = "3px";
    #endregion

    #region Storage
    public const string DarkModeStorageKey = "dark_mode";
    #endregion

    #region Reusable styles
    public const string NumberCellStyle = "text-align:right";
    public const string DropZoneStyle = "border-style:dashed;border-width:1px;border-color:var(--mud-palette-primary);";
    public const string FileInputOverlayStyle = "position:absolute;top:0;left:0;width:100%;height:100%;opacity:0;cursor:pointer;z-index:2;";
    #endregion

    #region Theme
    private static string[] HeadingFamily => [HeadingFont, "Georgia", "Times New Roman", "serif"];
    private static string[] BodyFamily => [BodyFont, "Segoe UI", "Helvetica", "Arial", "sans-serif"];

    public static readonly MudTheme Theme = new() {
        PaletteLight = new PaletteLight {
            Primary = AccentLight,
            PrimaryContrastText = "#ffffff",
            Secondary = MutedLight,
            Tertiary = "#8a5a00",
            Success = "#2d6a4f",
            Info = AccentLight,
            Warning = "#8a5a00",
            Error = "#9b2c2c",
            Background = PaperLight,
            Surface = SurfaceLight,
            AppbarBackground = PaperLight,
            AppbarText = InkLight,
            DrawerBackground = PaperLight,
            DrawerText = InkLight,
            TextPrimary = InkLight,
            TextSecondary = MutedLight,
            LinesDefault = RuleLight,
            LinesInputs = "#b4ad9b",
            Divider = RuleLight,
            DividerLight = RuleLight,
            ActionDefault = MutedLight
        },
        PaletteDark = new PaletteDark {
            Primary = AccentDark,
            PrimaryContrastText = "#0f1722",
            Secondary = MutedDark,
            Tertiary = "#d8a64a",
            Success = "#7bc4a0",
            Info = AccentDark,
            Warning = "#d8a64a",
            Error = "#e58a8a",
            Background = PaperDark,
            Surface = SurfaceDark,
            AppbarBackground = PaperDark,
            AppbarText = InkDark,
            DrawerBackground = PaperDark,
            DrawerText = InkDark,
            TextPrimary = InkDark,
            TextSecondary = MutedDark,
            LinesDefault = RuleDark,
            LinesInputs = "#4d4e55",
            Divider = RuleDark,
            DividerLight = RuleDark,
            ActionDefault = MutedDark
        },
        LayoutProperties = new LayoutProperties {
            DefaultBorderRadius = BorderRadius
        },
        Shadows = new Shadow {
            Elevation = Enumerable.Repeat("none", 26).ToArray()
        },
        Typography = new Typography {
            Default = new DefaultTypography { FontFamily = BodyFamily, FontSize = "0.95rem", FontWeight = "400", LineHeight = "1.6", LetterSpacing = "0" },
            H1 = new H1Typography { FontFamily = HeadingFamily, FontSize = "3rem", FontWeight = "600", LineHeight = "1.1", LetterSpacing = "-0.015em" },
            H2 = new H2Typography { FontFamily = HeadingFamily, FontSize = "2.4rem", FontWeight = "600", LineHeight = "1.12", LetterSpacing = "-0.01em" },
            H3 = new H3Typography { FontFamily = HeadingFamily, FontSize = "1.9rem", FontWeight = "600", LineHeight = "1.2", LetterSpacing = "-0.01em" },
            H4 = new H4Typography { FontFamily = HeadingFamily, FontSize = "1.6rem", FontWeight = "600", LineHeight = "1.25", LetterSpacing = "-0.005em" },
            H5 = new H5Typography { FontFamily = HeadingFamily, FontSize = "1.3rem", FontWeight = "600", LineHeight = "1.3", LetterSpacing = "0" },
            H6 = new H6Typography { FontFamily = HeadingFamily, FontSize = "1.1rem", FontWeight = "600", LineHeight = "1.35", LetterSpacing = "0" },
            Subtitle1 = new Subtitle1Typography { FontFamily = BodyFamily, FontSize = "1rem", FontWeight = "600", LineHeight = "1.5", LetterSpacing = "0" },
            Subtitle2 = new Subtitle2Typography { FontFamily = BodyFamily, FontSize = "0.9rem", FontWeight = "600", LineHeight = "1.5", LetterSpacing = "0" },
            Body1 = new Body1Typography { FontFamily = BodyFamily, FontSize = "0.95rem", FontWeight = "400", LineHeight = "1.65", LetterSpacing = "0" },
            Body2 = new Body2Typography { FontFamily = BodyFamily, FontSize = "0.875rem", FontWeight = "400", LineHeight = "1.6", LetterSpacing = "0" },
            Caption = new CaptionTypography { FontFamily = BodyFamily, FontSize = "0.78rem", FontWeight = "400", LineHeight = "1.5", LetterSpacing = "0.01em" },
            Overline = new OverlineTypography { FontFamily = BodyFamily, FontSize = "0.72rem", FontWeight = "600", LineHeight = "1.4", LetterSpacing = "0.14em" },
            Button = new ButtonTypography { FontFamily = BodyFamily, FontSize = "0.9rem", FontWeight = "600", LineHeight = "1.4", LetterSpacing = "0.01em", TextTransform = "none" }
        }
    };
    #endregion
}
