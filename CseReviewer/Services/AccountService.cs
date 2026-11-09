using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CseReviewer;
public record AccountSummary(string Id, string Username, string Email, string Role);
public record EditUserResult(AccountSummary Account, bool PasswordChanged);
public class AccountService(IDbContextFactory<StudyDb> dbFactory) {
    private static readonly PasswordHasher<AccountProfile> _hasher = new();
    private const string _usernamePattern = "^[a-z0-9_]{3,30}$";

    public async Task<ServiceResult<string>> Register(string? usernameInput, string? password) {
        var username = (usernameInput ?? "").Trim().ToLowerInvariant();
        if (username == "sa" || !Regex.IsMatch(username, _usernamePattern) || (password?.Length ?? 0) < 12 || password!.Length > 128)
            return ServiceResult<string>.Fail("Use a 3–30 character username (letters, numbers, underscores) and a password of 12–128 characters.");
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.AccountProfiles.AnyAsync(p => p.Username == username)) return ServiceResult<string>.Fail("That username is already taken.", 409);
        var profile = new AccountProfile { Id = Guid.NewGuid().ToString(), Username = username, Role = "User" };
        profile.PasswordHash = _hasher.HashPassword(profile, password);
        db.AccountProfiles.Add(profile);
        try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return ServiceResult<string>.Fail("Unable to create this username. Choose another or try signing in.", 409); }
        return ServiceResult<string>.Ok("Account created. Sign in with your username and password.");
    }

    public async Task<ServiceResult<AccountProfile>> Authenticate(string? login, string? password) {
        var username = (login ?? "").Trim().ToLowerInvariant();
        if (username.Length > 30 || string.IsNullOrEmpty(password) || password.Length > 128)
            return ServiceResult<AccountProfile>.Fail("Invalid username or password.", 401);
        await using var db = await dbFactory.CreateDbContextAsync();
        var profile = await db.AccountProfiles.SingleOrDefaultAsync(p => p.Username == username);
        if (profile == null || string.IsNullOrEmpty(profile.PasswordHash))
            return ServiceResult<AccountProfile>.Fail("Invalid username or password. For an old email account, ask the Super Admin to set a password.", 401);
        var result = _hasher.VerifyHashedPassword(profile, profile.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return ServiceResult<AccountProfile>.Fail("Invalid username or password.", 401);
        if (result == PasswordVerificationResult.SuccessRehashNeeded) {
            profile.PasswordHash = _hasher.HashPassword(profile, password);
            await db.SaveChangesAsync();
        }
        return ServiceResult<AccountProfile>.Ok(profile);
    }

    public async Task<ServiceResult<AccountProfile>> ChangeOwnPassword(string userId, string? currentPassword, string? newPassword) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var profile = await db.AccountProfiles.FindAsync(userId);
        if (profile == null || string.IsNullOrEmpty(profile.PasswordHash)) return ServiceResult<AccountProfile>.Fail("", 403);
        if (newPassword == null || newPassword.Length < 12 || newPassword.Length > 128 || currentPassword == null || currentPassword.Length > 128)
            return ServiceResult<AccountProfile>.Fail("Use a new password of 12–128 characters.");
        if (_hasher.VerifyHashedPassword(profile, profile.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            return ServiceResult<AccountProfile>.Fail("Current password is incorrect.");
        profile.PasswordHash = _hasher.HashPassword(profile, newPassword);
        profile.MustChangePassword = false;
        await db.SaveChangesAsync();
        return ServiceResult<AccountProfile>.Ok(profile);
    }

    public async Task<string?> GetRole(string userId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.AccountProfiles.Where(p => p.Id == userId).Select(p => p.Role).SingleOrDefaultAsync();
    }

    public async Task<ServiceResult<List<AccountSummary>>> GetUsers(string callerId) {
        if (await GetRole(callerId) != "SuperAdmin") return ServiceResult<List<AccountSummary>>.Fail("", 403);
        await using var db = await dbFactory.CreateDbContextAsync();
        var users = await db.AccountProfiles.OrderBy(p => p.Username).Select(p => new AccountSummary(p.Id, p.Username, p.Email, p.Role)).ToListAsync();
        return ServiceResult<List<AccountSummary>>.Ok(users);
    }

    public async Task<ServiceResult<AccountSummary>> UpdateAccount(string callerId, string id, UpdateAccount r) {
        if (await GetRole(callerId) != "SuperAdmin") return ServiceResult<AccountSummary>.Fail("", 403);
        var username = (r.Username ?? "").Trim().ToLowerInvariant();
        if (username == "sa" || !Regex.IsMatch(username, _usernamePattern))
            return ServiceResult<AccountSummary>.Fail("Use 3–30 letters, numbers or underscores. The username sa is reserved.");
        if (r.Role is not ("User" or "Admin")) return ServiceResult<AccountSummary>.Fail("Choose User or Admin.");
        if (!string.IsNullOrEmpty(r.Password) && (r.Password.Length < 12 || r.Password.Length > 128))
            return ServiceResult<AccountSummary>.Fail("Use a temporary password of 12–128 characters, or leave it blank to keep the current password.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var profile = await db.AccountProfiles.FindAsync(id);
        if (profile == null) return ServiceResult<AccountSummary>.Fail("Account not found.", 404);
        if (profile.Role == "SuperAdmin") return ServiceResult<AccountSummary>.Fail("The Super Admin account cannot be edited here.");
        if (await db.AccountProfiles.AnyAsync(p => p.Id != id && p.Username == username)) return ServiceResult<AccountSummary>.Fail("That username is already taken.", 409);
        profile.Username = username;
        profile.Role = r.Role;
        if (!string.IsNullOrEmpty(r.Password)) {
            profile.PasswordHash = _hasher.HashPassword(profile, r.Password);
            profile.MustChangePassword = true;
        }
        try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return ServiceResult<AccountSummary>.Fail("Unable to save this account. Choose another username or retry.", 409); }
        return ServiceResult<AccountSummary>.Ok(new AccountSummary(profile.Id, profile.Username, profile.Email, profile.Role));
    }

    public async Task<ServiceResult<string>> ChangeUsername(string callerId, string id, string? usernameInput) {
        if (await GetRole(callerId) != "SuperAdmin") return ServiceResult<string>.Fail("", 403);
        var username = (usernameInput ?? "").Trim().ToLowerInvariant();
        if (username == "sa" || !Regex.IsMatch(username, _usernamePattern))
            return ServiceResult<string>.Fail("Use 3–30 letters, numbers or underscores. The username sa is reserved.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var profile = await db.AccountProfiles.FindAsync(id);
        if (profile == null) return ServiceResult<string>.Fail("Account not found.", 404);
        if (profile.Role == "SuperAdmin") return ServiceResult<string>.Fail("The Super Admin username cannot be changed here.");
        if (await db.AccountProfiles.AnyAsync(p => p.Id != id && p.Username == username)) return ServiceResult<string>.Fail("That username is already taken.", 409);
        profile.Username = username;
        try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return ServiceResult<string>.Fail("Unable to save this username. Choose another or retry.", 409); }
        return ServiceResult<string>.Ok(username);
    }

    public async Task<ServiceResult> ChangeRole(string callerId, string id, string? role) {
        if (await GetRole(callerId) != "SuperAdmin") return ServiceResult.Fail("", 403);
        if (role is not ("Admin" or "User")) return ServiceResult.Fail("Choose Admin or User.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var profile = await db.AccountProfiles.FindAsync(id);
        if (profile == null) return ServiceResult.Fail("Account not found.", 404);
        if (profile.Role == "SuperAdmin") return ServiceResult.Fail("The Super Admin role cannot be changed here.");
        profile.Role = role;
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ResetPassword(string callerId, string id, string? password) {
        if (await GetRole(callerId) != "SuperAdmin") return ServiceResult.Fail("", 403);
        if (password == null || password.Length < 12 || password.Length > 128) return ServiceResult.Fail("Use a temporary password of 12–128 characters.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var profile = await db.AccountProfiles.FindAsync(id);
        if (profile == null) return ServiceResult.Fail("Account not found.", 404);
        if (profile.Role == "SuperAdmin") return ServiceResult.Fail("Use your own change-password form for Super Admin.");
        profile.PasswordHash = _hasher.HashPassword(profile, password);
        profile.MustChangePassword = true;
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
