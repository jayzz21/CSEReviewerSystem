using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace CseReviewer;
public class AccountProfile {
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "User";
    public string PasswordHash { get; set; } = "";
    public bool MustChangePassword { get; set; }
}
public record RegisterAccount(string Username, string Password);
public record LoginAccount(string Login, string Password);
public static class Accounts {
    public static async Task SignIn(HttpContext c, AccountProfile profile) {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, profile.Id), new(ClaimTypes.Name, profile.Username), new("verified_account", "true"), new(ClaimTypes.Role, profile.Role) };
        await c.SignInAsync("study", new ClaimsPrincipal(new ClaimsIdentity(claims, "study")), new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
    }
    public static void Map(WebApplication app) {
        AccountAdministration.Map(app);

        app.MapGet("/api/auth/config", () => new { configured = true, mode = "username" });
        var routes = app.MapGroup("/api/auth").RequireRateLimiting("auth");
        routes.MapPost("/register", async (RegisterAccount r, AccountService accounts) =>
            (await accounts.Register(r.Username, r.Password)).ToHttp(message => new { message }));
        routes.MapPost("/login", async (LoginAccount r, AccountService accounts, HttpContext c) => {
            var result = await accounts.Authenticate(r.Login, r.Password);
            if (!result.IsSuccess) return result.ToHttp();
            await SignIn(c, result.Value!);
            return Results.Ok(new { mustChangePassword = result.Value!.MustChangePassword });
        });
        // Older cached clients used POST here. Both methods only read an existing
        // authenticated session; neither creates an anonymous study account.
        app.MapMethods("/api/session", ["GET", "POST"], (HttpContext c) => {
            c.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { username = c.User.Identity!.Name, isAdmin = c.User.IsInRole("Admin") || c.User.IsInRole("SuperAdmin"), isSuperAdmin = c.User.IsInRole("SuperAdmin") });
        }).RequireAuthorization();
        app.MapPost("/api/auth/logout", async (HttpContext c) => { await c.SignOutAsync("study"); return Results.Ok(); });
    }
}
