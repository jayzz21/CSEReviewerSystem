using Microsoft.AspNetCore.Authentication;

namespace CseReviewer;
public static class AccountRoutes {
    public static void Map(WebApplication app) {
        app.MapGet("/Logout", async (HttpContext c) => {
            await c.SignOutAsync("study");
            return Results.Redirect("/Login");
        });
        MapOldAddresses(app);
    }

    private static void MapOldAddresses(WebApplication app) {
        app.MapGet("/login.html", (HttpContext c) => {
            if (c.Request.Query.ContainsKey("changePassword")) return Results.Redirect("/Change-Password");
            var lobby = LobbyCode(c.Request.Query["lobby"].ToString());
            if (lobby.Length == 0) return Results.Redirect("/Login");
            return Results.Redirect("/Login?returnUrl=" + Uri.EscapeDataString("/Multiplayer?code=" + lobby));
        });
        app.MapGet("/multiplayer.html", (HttpContext c) => {
            var lobby = LobbyCode(c.Request.Query["code"].ToString());
            return Results.Redirect(lobby.Length == 0 ? "/Multiplayer" : "/Multiplayer?code=" + lobby);
        });
        app.MapGet("/index.html", () => Results.Redirect("/"));
        app.MapGet("/admin.html", () => Results.Redirect("/Administration"));
    }

    private static string LobbyCode(string code) {
        return code.Length == 10 && code.All(Uri.IsHexDigit) ? code.ToUpperInvariant() : "";
    }
}
