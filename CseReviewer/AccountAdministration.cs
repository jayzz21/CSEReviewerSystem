using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CseReviewer;
public record ChangePassword(string CurrentPassword, string NewPassword);
public record ChangeRole(string Role);
public record ResetAccountPassword(string Password);
public record ChangeUsername(string Username);
public record UpdateAccount(string Username, string Role, string? Password);
public static class AccountAdministration {
    public static void Map(WebApplication app) {
        app.MapPost("/api/auth/change-password", async (ChangePassword r, StudyDb db, HttpContext c) => {
            var profile = await db.AccountProfiles.FindAsync(c.User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (profile == null || string.IsNullOrEmpty(profile.PasswordHash)) return Results.Forbid();
            if (r.NewPassword == null || r.NewPassword.Length < 12 || r.NewPassword.Length > 128 || r.CurrentPassword == null || r.CurrentPassword.Length > 128)
                return Results.BadRequest(new { error = "Use a new password of 12–128 characters." });
            var hasher = new PasswordHasher<AccountProfile>();
            if (hasher.VerifyHashedPassword(profile, profile.PasswordHash, r.CurrentPassword) == PasswordVerificationResult.Failed)
                return Results.BadRequest(new { error = "Current password is incorrect." });
            profile.PasswordHash = hasher.HashPassword(profile, r.NewPassword); profile.MustChangePassword = false;
            await db.SaveChangesAsync(); await Accounts.SignIn(c, profile); return Results.Ok();
        }).RequireAuthorization().RequireRateLimiting("auth");
        app.MapGet("/api/admin/users", async (StudyDb db) => await db.AccountProfiles.OrderBy(p => p.Username).Select(p => new { p.Id, p.Username, p.Email, p.Role }).ToListAsync()).RequireAuthorization("SuperAdmin");
        app.MapPost("/api/admin/users/{id}/update", async (string id, UpdateAccount r, StudyDb db) => {
            var username = (r.Username ?? "").Trim().ToLowerInvariant();
            if (username == "sa" || !System.Text.RegularExpressions.Regex.IsMatch(username, "^[a-z0-9_]{3,30}$"))
                return Results.BadRequest(new { error = "Use 3–30 letters, numbers or underscores. The username sa is reserved." });
            if (r.Role is not ("User" or "Admin")) return Results.BadRequest(new { error = "Choose User or Admin." });
            if (!string.IsNullOrEmpty(r.Password) && (r.Password.Length < 12 || r.Password.Length > 128))
                return Results.BadRequest(new { error = "Use a temporary password of 12–128 characters, or leave it blank to keep the current password." });
            var profile = await db.AccountProfiles.FindAsync(id);
            if (profile == null) return Results.NotFound();
            if (profile.Role == "SuperAdmin") return Results.BadRequest(new { error = "The Super Admin account cannot be edited here." });
            if (await db.AccountProfiles.AnyAsync(p => p.Id != id && p.Username == username)) return Results.Conflict(new { error = "That username is already taken." });
            profile.Username = username; profile.Role = r.Role;
            if (!string.IsNullOrEmpty(r.Password)) {
                profile.PasswordHash = new PasswordHasher<AccountProfile>().HashPassword(profile, r.Password);
                profile.MustChangePassword = true;
            }
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return Results.Conflict(new { error = "Unable to save this account. Choose another username or retry." }); }
            return Results.Ok(new { profile.Username, profile.Role });
        }).RequireAuthorization("SuperAdmin");
        app.MapPost("/api/admin/users/{id}/username", async (string id, ChangeUsername r, StudyDb db) => {
            var username = (r.Username ?? "").Trim().ToLowerInvariant();
            if (username == "sa" || !System.Text.RegularExpressions.Regex.IsMatch(username, "^[a-z0-9_]{3,30}$"))
                return Results.BadRequest(new { error = "Use 3–30 letters, numbers or underscores. The username sa is reserved." });
            var profile = await db.AccountProfiles.FindAsync(id);
            if (profile == null) return Results.NotFound();
            if (profile.Role == "SuperAdmin") return Results.BadRequest(new { error = "The Super Admin username cannot be changed here." });
            if (await db.AccountProfiles.AnyAsync(p => p.Id != id && p.Username == username)) return Results.Conflict(new { error = "That username is already taken." });
            profile.Username = username;
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return Results.Conflict(new { error = "Unable to save this username. Choose another or retry." }); }
            return Results.Ok(new { username });
        }).RequireAuthorization("SuperAdmin");
        app.MapPost("/api/admin/users/{id}/role", async (string id, ChangeRole r, StudyDb db) => {
            if (r.Role is not ("Admin" or "User")) return Results.BadRequest(new { error = "Choose Admin or User." });
            var profile = await db.AccountProfiles.FindAsync(id);
            if (profile == null) return Results.NotFound();
            if (profile.Role == "SuperAdmin") return Results.BadRequest(new { error = "The Super Admin role cannot be changed here." });
            profile.Role = r.Role; await db.SaveChangesAsync(); return Results.Ok();
        }).RequireAuthorization("SuperAdmin");
        app.MapPost("/api/admin/users/{id}/password", async (string id, ResetAccountPassword r, StudyDb db) => {
            if (r.Password == null || r.Password.Length < 12 || r.Password.Length > 128) return Results.BadRequest(new { error = "Use a temporary password of 12–128 characters." });
            var profile = await db.AccountProfiles.FindAsync(id);
            if (profile == null) return Results.NotFound();
            if (profile.Role == "SuperAdmin") return Results.BadRequest(new { error = "Use your own change-password form for Super Admin." });
            profile.PasswordHash = new PasswordHasher<AccountProfile>().HashPassword(profile, r.Password);
            profile.MustChangePassword = true;
            await db.SaveChangesAsync(); return Results.Ok();
        }).RequireAuthorization("SuperAdmin").RequireRateLimiting("auth");
    }
}
