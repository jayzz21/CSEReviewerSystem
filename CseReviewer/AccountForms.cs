using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace CseReviewer;
public static class AccountForms {
    public static void Map(WebApplication app) {
        app.MapPost("/Account/Login", async ([FromForm] string? login, [FromForm] string? password, [FromForm] string? returnUrl, AccountService accounts, HttpContext c) => {
            var safeReturn = SafeReturnUrl(returnUrl);
            var result = await accounts.Authenticate(login, password);
            if (!result.IsSuccess) {
                var errorCode = result.Error.Contains("old email") ? "legacy" : "invalid";
                return Results.Redirect("/Login?error=" + errorCode + ReturnUrlQuery(safeReturn));
            }
            await Accounts.SignIn(c, result.Value!);
            if (result.Value!.MustChangePassword) return Results.Redirect("/Change-Password" + (safeReturn == "/" ? "" : "?returnUrl=" + Uri.EscapeDataString(safeReturn)));
            return Results.Redirect(safeReturn);
        }).RequireRateLimiting("auth");

        app.MapPost("/Account/Register", async ([FromForm] string? username, [FromForm] string? password, AccountService accounts) => {
            var result = await accounts.Register(username, password);
            if (result.IsSuccess) return Results.Redirect("/Login?registered=1");
            return Results.Redirect("/Register?error=" + (result.Status == 409 ? "taken" : "invalid"));
        }).RequireRateLimiting("auth");

        app.MapPost("/Account/ChangePassword", async ([FromForm] string? currentPassword, [FromForm] string? newPassword, [FromForm] string? returnUrl, AccountService accounts, HttpContext c) => {
            var safeReturn = SafeReturnUrl(returnUrl);
            var result = await accounts.ChangeOwnPassword(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!, currentPassword, newPassword);
            if (!result.IsSuccess) {
                var errorCode = result.Error.Contains("incorrect") ? "wrong" : "invalid";
                return Results.Redirect("/Change-Password?error=" + errorCode + ReturnUrlQuery(safeReturn));
            }
            await Accounts.SignIn(c, result.Value!);
            return Results.Redirect(safeReturn);
        }).RequireAuthorization().RequireRateLimiting("auth");

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

    private static string SafeReturnUrl(string? url) {
        var isLocalPath = !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");
        return isLocalPath ? url! : "/";
    }

    private static string ReturnUrlQuery(string safeReturn) {
        return safeReturn == "/" ? "" : "&returnUrl=" + Uri.EscapeDataString(safeReturn);
    }
}
