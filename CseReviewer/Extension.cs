using MudBlazor;

namespace CseReviewer;
public static class Extension {
    public static void ShowAlert(string message, Variant variant, ISnackbar snackbar, Severity severity) {
        snackbar.Add(message, severity, options => options.SnackbarVariant = variant);
    }
}
