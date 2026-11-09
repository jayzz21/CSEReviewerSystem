using System.Security.Claims;

namespace CseReviewer;
public record ChangePassword(string CurrentPassword, string NewPassword);
public record ChangeRole(string Role);
public record ResetAccountPassword(string Password);
public record ChangeUsername(string Username);
public record UpdateAccount(string Username, string Role, string? Password);
public static class AccountAdministration {
    public static void Map(WebApplication app) {
        app.MapPost("/api/auth/change-password", async (ChangePassword r, AccountService accounts, HttpContext c) => {
            var result = await accounts.ChangeOwnPassword(UserId(c), r.CurrentPassword, r.NewPassword);
            if (!result.IsSuccess) return result.ToHttp();
            await Accounts.SignIn(c, result.Value!);
            return Results.Ok();
        }).RequireAuthorization().RequireRateLimiting("auth");
        app.MapGet("/api/admin/users", async (AccountService accounts, HttpContext c) =>
            (await accounts.GetUsers(UserId(c))).ToHttp()).RequireAuthorization("SuperAdmin");
        app.MapPost("/api/admin/users/{id}/update", async (string id, UpdateAccount r, AccountService accounts, HttpContext c) =>
            (await accounts.UpdateAccount(UserId(c), id, r)).ToHttp(account => new { account.Username, account.Role })).RequireAuthorization("SuperAdmin");
        app.MapPost("/api/admin/users/{id}/username", async (string id, ChangeUsername r, AccountService accounts, HttpContext c) =>
            (await accounts.ChangeUsername(UserId(c), id, r.Username)).ToHttp(username => new { username })).RequireAuthorization("SuperAdmin");
        app.MapPost("/api/admin/users/{id}/role", async (string id, ChangeRole r, AccountService accounts, HttpContext c) =>
            (await accounts.ChangeRole(UserId(c), id, r.Role)).ToHttp()).RequireAuthorization("SuperAdmin");
        app.MapPost("/api/admin/users/{id}/password", async (string id, ResetAccountPassword r, AccountService accounts, HttpContext c) =>
            (await accounts.ResetPassword(UserId(c), id, r.Password)).ToHttp()).RequireAuthorization("SuperAdmin").RequireRateLimiting("auth");
    }

    private static string UserId(HttpContext c) => c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
