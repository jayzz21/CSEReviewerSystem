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
        routes.MapPost("/register", async (RegisterAccount r, StudyDb db) => {
            var username = (r.Username ?? "").Trim().ToLowerInvariant();
            if (username == "sa" || !Regex.IsMatch(username, "^[a-z0-9_]{3,30}$") || (r.Password?.Length ?? 0) < 12 || r.Password!.Length > 128)
                return Results.BadRequest(new { error = "Use a 3–30 character username (letters, numbers, underscores) and a password of 12–128 characters." });
            if (await db.AccountProfiles.AnyAsync(p => p.Username == username)) return Results.Conflict(new { error = "That username is already taken." });
            var profile = new AccountProfile { Id = Guid.NewGuid().ToString(), Username = username, Role = "User" };
            profile.PasswordHash = new PasswordHasher<AccountProfile>().HashPassword(profile, r.Password);
            db.AccountProfiles.Add(profile);
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return Results.Conflict(new { error = "Unable to create this username. Choose another or try signing in." }); }
            return Results.Ok(new { message = "Account created. Sign in with your username and password." });
        });
        routes.MapPost("/login", async (LoginAccount r, StudyDb db, HttpContext c) => {
            var username = (r.Login ?? "").Trim().ToLowerInvariant();
            if (username.Length > 30 || string.IsNullOrEmpty(r.Password) || r.Password.Length > 128)
                return Results.Json(new { error = "Invalid username or password." }, statusCode: 401);
            var profile = await db.AccountProfiles.SingleOrDefaultAsync(p => p.Username == username);
            if (profile == null || string.IsNullOrEmpty(profile.PasswordHash)) return Results.Json(new { error = "Invalid username or password. For an old email account, ask the Super Admin to set a password." }, statusCode: 401);
            var hasher = new PasswordHasher<AccountProfile>();
            var result = hasher.VerifyHashedPassword(profile, profile.PasswordHash, r.Password);
            if (result == PasswordVerificationResult.Failed) return Results.Json(new { error = "Invalid username or password." }, statusCode: 401);
            if (result == PasswordVerificationResult.SuccessRehashNeeded) { profile.PasswordHash = hasher.HashPassword(profile, r.Password); await db.SaveChangesAsync(); }
            await SignIn(c, profile);
            return Results.Ok(new { mustChangePassword = profile.MustChangePassword });
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
