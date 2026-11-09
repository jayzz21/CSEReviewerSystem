using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CseReviewer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using System.Text;
using Microsoft.Extensions.Configuration;

var database = "CseReviewerAuthCheck_" + Guid.NewGuid().ToString("N");
var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
var root = Path.GetFullPath("CseReviewer");
var server = new Process { StartInfo = new ProcessStartInfo("dotnet", $"\"{Path.Combine(root, "bin/AuthCheck/CseReviewer.dll")}\" --urls http://localhost:5096") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
server.StartInfo.Environment["ConnectionStrings__Study"] = connection;
server.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
server.OutputDataReceived += (_, _) => { }; server.ErrorDataReceived += (_, _) => { };
var services = new ServiceCollection();
services.AddLogging(); services.AddDbContext<StudyDb>(o => o.UseSqlServer(connection));
services.AddAuthentication("study").AddCookie("study");
services.AddDataProtection().SetApplicationName("Husay.Accounts.v1").PersistKeysToDbContext<StudyDb>();
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<StudyDb>();
HttpClient Client(string? id = null, string role = "User", bool verified = true) {
    var cookies = new CookieContainer();
    if (id != null) {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id), new(ClaimTypes.Role, role) };
        if (verified) claims.Add(new("verified_account", "true"));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "study")), new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10) }, "study");
        var protector = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware", "study", "v2");
        cookies.Add(new Uri("http://localhost:5096"), new Cookie("cse.study", new TicketDataFormat(protector).Protect(ticket)));
    }
    return new HttpClient(new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false }) { BaseAddress = new Uri("http://localhost:5096") };
}
void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
try {
    server.Start(); server.BeginOutputReadLine(); server.BeginErrorReadLine();
    using var sa = Client();
    var ready = false;
    for (var i = 0; i < 60; i++) { try { ready = (await sa.GetAsync("/api/auth/config")).IsSuccessStatusCode; } catch (HttpRequestException) { } if (ready) break; await Task.Delay(500); }
    Check(ready, "Isolated test server started");
    var notifier = new LobbyNotifier(); var notified = 0;
    using (notifier.Subscribe("AABBCCDDEE", () => { Interlocked.Increment(ref notified); return Task.CompletedTask; })) { notifier.Notify("AABBCCDDEE"); await Task.Delay(200); }
    notifier.Notify("AABBCCDDEE"); await Task.Delay(200);
    Check(notified == 1, "Lobby notifier reaches subscribers until they unsubscribe");
    var home = await sa.GetAsync("/");
    Check(home.StatusCode == HttpStatusCode.Redirect && home.Headers.Location?.ToString() == "/Login?returnUrl=%2F", "Front page redirects anonymous visitors to login without JavaScript");
    Check((await sa.GetAsync("/index.html")).Headers.Location?.ToString() == "/", "Old reviewer address redirects to the front page");
    Check((await sa.GetAsync("/?view=multiplayer&code=AABBCCDDEE")).Headers.Location?.ToString()?.Contains("AABBCCDDEE") == true, "Shared lobby survives the signed-out front-page redirect");
    Check((await sa.GetAsync("/Multiplayer?code=AABBCCDDEE")).Headers.Location?.ToString() == "/Login?returnUrl=%2FMultiplayer%3Fcode%3DAABBCCDDEE", "Shared lobby survives the signed-out multiplayer page redirect");
    Check((await sa.GetAsync("/login.html?lobby=AABBCCDDEE")).Headers.Location?.ToString() == "/Login?returnUrl=%2FMultiplayer%3Fcode%3DAABBCCDDEE", "Old login address with a lobby code still reaches the lobby after sign-in");
    Check((await sa.GetStringAsync("/Login")).Contains("_framework/blazor.web.js"), "Login page is served publicly");
    Check((await sa.GetStringAsync("/Register")).Contains("_framework/blazor.web.js"), "Registration page is served publicly");
    Check((await sa.GetAsync("/api/sets")).StatusCode == HttpStatusCode.Unauthorized, "Anonymous study access denied");
    Check((await sa.PostAsJsonAsync("/api/session", new { })).StatusCode == HttpStatusCode.Unauthorized, "Legacy POST session requires login instead of returning 405");
    var login = await sa.PostAsJsonAsync("/api/auth/login", new { login = "sa", password = "123" });
    Check(login.IsSuccessStatusCode, "Initial Super Admin login");
    Check((await sa.GetAsync("/")).Headers.Location?.ToString() == "/Change-Password?returnUrl=%2F", "Front page directs initial admin to password setup");
    Check((await sa.GetAsync("/api/admin/users")).StatusCode == HttpStatusCode.Forbidden, "Initial password must be changed before admin access");
    Check((await sa.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "123", newPassword = "Temporary-test-only-42!" })).IsSuccessStatusCode, "Super Admin password change");
    Check((await sa.GetAsync("/")).IsSuccessStatusCode, "Authenticated front page serves reviewer");
    Check((await sa.GetStringAsync("/Multiplayer")).Contains("_framework/blazor.web.js"), "Authenticated users get the Razor multiplayer page");
    var aiRequest = await sa.PostAsJsonAsync("/api/sets", new { category = "Numerical Ability", level = "Professional", difficulty = "Easy", count = 5, focus = "", mode = "ai" });
    Check(aiRequest.StatusCode == HttpStatusCode.ServiceUnavailable && (await aiRequest.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString() == "ai_not_available", "AI mode returns explicit unavailable message");
    Check(!await db.Sets.AnyAsync(), "Unavailable AI request does not create a set");
    foreach (var page in new[] { "/My-Saved-Sets", "/Administration" }) Check((await sa.GetStringAsync(page)).Contains("_framework/blazor.web.js"), $"Authenticated users get the Razor page {page}");
    var legacySession = await sa.PostAsJsonAsync("/api/session", new { });
    Check(legacySession.IsSuccessStatusCode && (await legacySession.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("username").GetString() == "sa", "Legacy POST session returns authenticated account");
    db.AccountProfiles.AddRange(new AccountProfile { Id = "test-user-a", Username = "user_a", Email = "a@example.invalid" }, new AccountProfile { Id = "test-user-b", Username = "user_b", Email = "b@example.invalid" });
    var set = new StudySet { Owner = "test-user-a", Title = "Private", Category = "Numerical Ability", Level = "Professional", Difficulty = "Easy", Source = "Sample", Count = 2, Questions = JsonSerializer.Serialize(new[] { new Question("1+1?", ["2","3","4","5"], 0,"Addition", ["1+1=2"]), new Question("2+2?", ["2","3","4","5"], 2,"Addition", ["2+2=4"]) }) };
    db.Sets.Add(set); await db.SaveChangesAsync();
    using var a = Client("test-user-a", "SuperAdmin"); using var b = Client("test-user-b"); using var legacy = Client("test-user-a", verified: false);
    Check((await legacy.GetAsync("/api/session")).StatusCode == HttpStatusCode.Unauthorized, "Old anonymous cookies rejected");
    Check((await a.GetAsync("/api/admin/users")).StatusCode == HttpStatusCode.Forbidden, "Cookie role cannot override database role");
    Check((await a.PostAsJsonAsync("/api/imports", new { fileName = "x.html", html = "" })).StatusCode == HttpStatusCode.Forbidden, "Ordinary user import denied");
    Check((await a.PostAsJsonAsync("/api/imports/preview", new { fileName = "x.html", html = "" })).StatusCode == HttpStatusCode.Forbidden, "Ordinary user import preview denied");
    Check((await a.PostAsJsonAsync("/api/admin/users/test-user-b/role", new { role = "Admin" })).StatusCode == HttpStatusCode.Forbidden, "Ordinary user role assignment denied");
    Check((await a.PostAsJsonAsync($"/api/sets/{set.Id}/progress", new { choices = new[] { 0 } })).IsSuccessStatusCode, "Own progress saved");
    var resumed = await a.GetFromJsonAsync<JsonElement>($"/api/sets/{set.Id}");
    Check(resumed.GetProperty("progress").GetArrayLength() == 1, "Saved progress restored");
    Check((await b.GetAsync($"/api/sets/{set.Id}")).StatusCode == HttpStatusCode.NotFound, "Other user cannot read private set");
    Check((await b.PostAsJsonAsync($"/api/sets/{set.Id}/progress", new { choices = new[] { 1 } })).StatusCode == HttpStatusCode.NotFound, "Other user cannot overwrite progress");
    Check((await b.GetFromJsonAsync<JsonElement>("/api/sets")).GetArrayLength() == 0, "Other user's library excludes private sets");
    Check((await sa.PostAsJsonAsync("/api/admin/users/test-user-a/role", new { role = "Admin" })).IsSuccessStatusCode, "Super Admin assigns employee role");
    Check((await a.PostAsJsonAsync("/api/imports/preview", new { fileName = "x.html", html = "" })).StatusCode == HttpStatusCode.BadRequest, "Admin reaches import validation");
    Check((await a.GetAsync("/api/admin/users")).StatusCode == HttpStatusCode.Forbidden, "Employee Admin cannot manage roles");
    await sa.PostAsJsonAsync("/api/admin/users/test-user-a/role", new { role = "User" });
    Check((await a.PostAsJsonAsync("/api/imports/preview", new { fileName = "x.html", html = "" })).StatusCode == HttpStatusCode.Forbidden, "Role revocation applies to existing sessions");
    await a.PostAsJsonAsync("/api/auth/logout", new { });
    Check((await a.GetAsync("/api/session")).StatusCode == HttpStatusCode.Unauthorized, "Logout clears access");
    using var registered = Client();
    var registration = await registered.PostAsJsonAsync("/api/auth/register", new { username = "new_student", password = "Student-test-password-42!", role = "SuperAdmin" });
    Check(registration.IsSuccessStatusCode, "Username-only registration without email or provider configuration");
    var newProfile = await db.AccountProfiles.AsNoTracking().SingleAsync(p => p.Username == "new_student");
    Check(newProfile.Role == "User" && newProfile.Email == "" && newProfile.PasswordHash != "Student-test-password-42!", "Registration stores a hash and cannot grant admin roles");
    Check((await registered.PostAsJsonAsync("/api/auth/register", new { username = "NEW_STUDENT", password = "Student-test-password-42!" })).StatusCode == HttpStatusCode.Conflict, "Duplicate username rejected case-insensitively");
    Check((await registered.PostAsJsonAsync("/api/auth/login", new { login = "new_student", password = "incorrect" })).StatusCode == HttpStatusCode.Unauthorized, "Wrong password rejected");
    Check((await registered.PostAsJsonAsync("/api/auth/login", new { login = "NEW_STUDENT", password = "Student-test-password-42!" })).IsSuccessStatusCode, "New user signs in immediately with username and password");
    Check((await registered.GetAsync("/api/admin/users")).StatusCode == HttpStatusCode.Forbidden, "Registered user cannot manage roles");
    Check((await registered.GetAsync("/api/auth/google")).StatusCode == HttpStatusCode.NotFound, "Google login endpoint removed");
    Check((await registered.PostAsJsonAsync("/api/admin/users/test-user-b/password", new { password = "Temporary-password-42!" })).StatusCode == HttpStatusCode.Forbidden, "User cannot reset another account password");
    Check((await sa.PostAsJsonAsync("/api/admin/users/test-user-b/password", new { password = "Temporary-password-42!" })).IsSuccessStatusCode, "Super Admin enables username password for an existing email account");
    using var migrated = Client();
    Check((await migrated.PostAsJsonAsync("/api/auth/login", new { login = "user_b", password = "Temporary-password-42!" })).IsSuccessStatusCode, "Existing account signs in after password reset");
    Check((await migrated.GetAsync("/api/sets")).StatusCode == HttpStatusCode.Forbidden, "Reset account must change temporary password");
    Check((await migrated.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "Temporary-password-42!", newPassword = "Personal-password-84!" })).IsSuccessStatusCode, "Regular user replaces temporary password");
    using var multiplayerHost = Client("test-user-a");
    using var multiplayerGuest = Client("test-user-b");
    await MultiplayerChecks.Run(db, multiplayerHost, multiplayerGuest, registered, Check);
    Check((await multiplayerHost.PostAsJsonAsync("/api/admin/users/test-user-b/username", new { username = "renamed_user" })).StatusCode == HttpStatusCode.Forbidden, "Ordinary users cannot rename accounts");
    Check((await sa.PostAsJsonAsync("/api/admin/users/test-user-b/username", new { username = "sa" })).StatusCode == HttpStatusCode.BadRequest, "Reserved admin username is protected");
    Check((await sa.PostAsJsonAsync("/api/admin/users/test-user-b/username", new { username = "new_student" })).StatusCode == HttpStatusCode.Conflict, "Duplicate username is rejected");
    (await sa.PostAsJsonAsync("/api/admin/users/test-user-b/username", new { username = "Renamed_User" })).EnsureSuccessStatusCode();
    Check((await multiplayerGuest.GetFromJsonAsync<JsonElement>("/api/session")).GetProperty("username").GetString() == "renamed_user", "Renamed username is normalized and visible in existing sessions");
    Check((await migrated.PostAsJsonAsync("/api/auth/login", new { login = "renamed_user", password = "Personal-password-84!" })).IsSuccessStatusCode, "Renaming preserves password and permits login with the new username");
    Check((await multiplayerHost.PostAsJsonAsync("/api/admin/users/test-user-b/update", new { username = "changed_user", role = "Admin", password = "" })).StatusCode == HttpStatusCode.Forbidden, "Combined account updates require Super Admin");
    Check((await sa.PostAsJsonAsync("/api/admin/users/test-user-b/update", new { username = "changed_user", role = "Admin", password = "short" })).StatusCode == HttpStatusCode.BadRequest, "Invalid password rejects the complete account update");
    var beforeUpdate = await db.AccountProfiles.AsNoTracking().SingleAsync(p => p.Id == "test-user-b");
    Check(beforeUpdate.Username == "renamed_user" && beforeUpdate.Role == "User", "Failed update leaves username and role unchanged");
    (await sa.PostAsJsonAsync("/api/admin/users/test-user-b/update", new { username = "changed_user", role = "Admin", password = "" })).EnsureSuccessStatusCode();
    var afterUpdate = await db.AccountProfiles.AsNoTracking().SingleAsync(p => p.Id == "test-user-b");
    Check(afterUpdate.Username == "changed_user" && afterUpdate.Role == "Admin" && afterUpdate.PasswordHash == beforeUpdate.PasswordHash, "Single save updates username and role while blank password preserves the hash");
    (await sa.PostAsJsonAsync("/api/admin/users/test-user-b/update", new { username = "changed_user", role = "User", password = "New-temporary-password-42!" })).EnsureSuccessStatusCode();
    afterUpdate = await db.AccountProfiles.AsNoTracking().SingleAsync(p => p.Id == "test-user-b");
    Check(afterUpdate.MustChangePassword && afterUpdate.PasswordHash != beforeUpdate.PasswordHash && afterUpdate.Role == "User", "Single save can reset password and require a change at next sign-in");
    foreach (var difficulty in new[] { "Moderate", "Hard" })
        for (var i = 0; i < 6; i++) {
            var entry = new ImportEntry("Numerical Ability", "Professional", difficulty, $"Mixed check {difficulty} {i}", ["A", "B", "C", "D"], 0, "Explanation", []);
            db.BankQuestions.Add(new BankQuestion { Owner = "test-bank", Fingerprint = HtmlImport.Fingerprint(entry), Category = entry.Category, Level = entry.Level, Difficulty = difficulty, Data = JsonSerializer.Serialize(entry) });
        }
    await db.SaveChangesAsync();
    async Task<int> Available(string difficulty, string category = "Numerical Ability") {
        var response = await multiplayerHost.PostAsJsonAsync("/api/bank/check", new { category, level = "Professional", difficulty, count = 1 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("available").GetInt32();
    }
    Check(await Available("Mixed") == 57 && await Available("Easy") == 45 && await Available("Hard") == 6, "Bank counts respect Mixed and individual difficulty filters");
    Check(await Available("Hard", "Verbal Ability") == 0, "Empty difficulty does not fall back to other questions");
    var largeBankSet = await multiplayerHost.PostAsJsonAsync("/api/bank/sets", new { category = "All 4 Subjects (Final Mock Exam)", level = "Professional", difficulty = "Mixed", count = 190 });
    Check(largeBankSet.IsSuccessStatusCode && (await largeBankSet.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("count").GetInt32() == 190, "Database sets can exceed 180 when matching questions exist");
    Check((await multiplayerHost.PostAsJsonAsync("/api/bank/sets", new { category = "Numerical Ability", level = "Professional", difficulty = "Hard", count = 7 })).StatusCode == HttpStatusCode.BadRequest, "Database rejects requests above matching difficulty availability");

} finally {
    if (!server.HasExited) { server.Kill(); await server.WaitForExitAsync(); }
    // Only this randomly named, isolated verification database is removed.
    if (database.StartsWith("CseReviewerAuthCheck_")) { Microsoft.Data.SqlClient.SqlConnection.ClearAllPools(); await db.Database.EnsureDeletedAsync(); }
    server.Dispose();
}
