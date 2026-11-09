using MudBlazor;

namespace CseReviewer.Components.Layout;
public static class AppTheme {
    #region Colors
    public const string PrimaryLight = "#115e59";
    public const string PrimaryDark = "#5eead4";
    public const string SecondaryLight = "#475569";
    public const string SecondaryDark = "#94a3b8";
    #endregion

    #region Layout
    public const MaxWidth PageMaxWidth = MaxWidth.Large;
    public const string PageSpacing = "my-6";
    public const string CardPadding = "pa-6";
    public const string LargeCardPadding = "pa-6 pa-md-8";
    public const string SectionGap = "mt-6";
    public const string DrawerWidth = "260px";
    public const string BorderRadius = "10px";
    #endregion

    #region Storage
    public const string DarkModeStorageKey = "dark_mode";
    #endregion

    #region Reusable styles
    public const string ChoiceButtonStyle = "justify-content:flex-start;text-align:left;text-transform:none;padding:12px 16px;";
    public const string CategoryButtonStyle = "justify-content:flex-start;text-align:left;text-transform:none;padding:12px 16px;height:100%;";
    public const string DropZoneStyle = "border-style:dashed;border-width:2px;border-color:var(--mud-palette-primary);";
    public const string FileInputOverlayStyle = "position:absolute;top:0;left:0;width:100%;height:100%;opacity:0;cursor:pointer;z-index:2;";
    public const string NumberCellStyle = "text-align:right";
    #endregion

    #region Theme
    public static readonly MudTheme Theme = new() {
        PaletteLight = new PaletteLight {
            Primary = PrimaryLight,
            Secondary = SecondaryLight,
            Background = "#f8fafc",
            AppbarBackground = PrimaryLight,
            AppbarText = "#ffffff",
            DrawerBackground = "#ffffff"
        },
        PaletteDark = new PaletteDark {
            Primary = PrimaryDark,
            Secondary = SecondaryDark,
            Background = "#181a21",
            Surface = "#282c37",
            AppbarBackground = "#1e222d",
            DrawerBackground = "#1e222d"
        },
        LayoutProperties = new LayoutProperties {
            DefaultBorderRadius = BorderRadius,
            DrawerWidthLeft = DrawerWidth
        },
        Typography = new Typography {
            Default = new DefaultTypography {
                FontFamily = ["Roboto", "Helvetica", "Arial", "sans-serif"]
            }
        }
    };
    #endregion
}
