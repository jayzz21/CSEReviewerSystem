using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using CseReviewer;
using CseReviewer.Components;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
var connStr = builder.Configuration.GetConnectionString("Study") ?? "";
bool isPostgres = connStr.Contains("supabase", StringComparison.OrdinalIgnoreCase) ||
                  connStr.Contains("postgres", StringComparison.OrdinalIgnoreCase) ||
                  connStr.StartsWith("Host=", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContextFactory<StudyDb>(o => {
    if (isPostgres) {
        o.UseNpgsql(NormalizePostgresConnString(connStr));
    } else {
        o.UseSqlServer(connStr);
    }
});
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<StudyDb>>().CreateDbContext());
builder.Services.AddAuthentication("study").AddCookie("study", o => {
    o.Cookie.Name = "cse.study"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
    o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = false;
    o.Events.OnValidatePrincipal = async c => {
        var db = c.HttpContext.RequestServices.GetRequiredService<StudyDb>();
        var id = c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var profile = id == null ? null : await db.AccountProfiles.FindAsync(id);
        if (c.Principal?.FindFirstValue("verified_account") != "true" || profile == null) { c.RejectPrincipal(); return; }
        var identity = (ClaimsIdentity)c.Principal.Identity!;
        foreach (var name in identity.FindAll(ClaimTypes.Name).ToList()) identity.RemoveClaim(name);
        identity.AddClaim(new Claim(ClaimTypes.Name, profile.Username));
        foreach (var role in identity.FindAll(ClaimTypes.Role).ToList()) identity.RemoveClaim(role);
        identity.AddClaim(new Claim(ClaimTypes.Role, profile.Role));
        if (profile.MustChangePassword) identity.AddClaim(new Claim("must_change_password", "true"));
    };
    o.Events.OnRedirectToAccessDenied = c => {
        if (c.Request.Path.StartsWithSegments("/api")) c.Response.StatusCode = 403;
        else c.Response.Redirect("/");
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToLogin = c => {
        if (c.Request.Path.StartsWithSegments("/api")) c.Response.StatusCode = 401;
        else c.Response.Redirect("/Login?returnUrl=" + Uri.EscapeDataString(c.Request.Path + c.Request.QueryString));
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization(o => {
    o.AddPolicy("Admin", p => p.RequireRole("Admin", "SuperAdmin"));
    o.AddPolicy("SuperAdmin", p => p.RequireRole("SuperAdmin"));
});
builder.Services.AddDataProtection().SetApplicationName("Husay.Accounts.v1").PersistKeysToDbContext<StudyDb>();

builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddHttpClient<QuestionGenerator>(c => c.Timeout = TimeSpan.FromMinutes(5));
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddMudServices();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddScoped<PracticeService>();
builder.Services.AddScoped<BankService>();
builder.Services.AddScoped<LobbyService>();
builder.Services.AddSingleton<LobbyNotifier>();
var app = builder.Build();
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<StudyDb>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync(db.Database.IsSqlServer() ? AccountSchema.SqlServer : AccountSchema.Postgres);
    await db.Database.ExecuteSqlRawAsync(db.Database.IsSqlServer() ? Multiplayer.SqlServer : Multiplayer.Postgres);
    if (!await db.AccountProfiles.AnyAsync(p => p.Id == "local-super-admin")) {
        var sa = new AccountProfile { Id = "local-super-admin", Username = "sa", Role = "SuperAdmin", MustChangePassword = true };
        sa.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<AccountProfile>().HashPassword(sa, "123");
        db.AccountProfiles.Add(sa); await db.SaveChangesAsync();
    }
    if (db.Database.IsSqlServer()) {
        await db.Database.ExecuteSqlRawAsync(ImportedBank.Schema);
        await db.Database.ExecuteSqlRawAsync(PracticeProgress.Schema);
    }
}
app.Use(async (ctx, next) => {
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    if (ctx.Request.Method == "POST" && ctx.Request.Path.StartsWithSegments("/api") && !ctx.Request.HasJsonContentType()) { ctx.Response.StatusCode = 415; return; }
    try { await next(); }
    catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) {
        // The caller left the page; do not attempt to write to a closed connection.
    }
    catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidDataException or AiConfigurationException) {
        if (ctx.Request.Path.StartsWithSegments("/api/auth")) {
            app.Logger.LogWarning("Authentication provider request failed ({Type})", ex.GetType().Name);
            ctx.Response.StatusCode = 503;
            await ctx.Response.WriteAsJsonAsync(new { error = "The sign-in service is temporarily unavailable. Please try again." });
            return;
        }
        app.Logger.LogWarning(ex, "Question generation failed");
        if (!ctx.Response.HasStarted) {
            var failure = GenerationFailure.From(ex, AiSettings.Model(builder.Configuration), AiSettings.Provider(builder.Configuration));
            ctx.Response.StatusCode = failure.Status;
            await ctx.Response.WriteAsJsonAsync(new { error = failure.Error, code = failure.Code });
        }
    }
});
app.UseAuthentication();
app.Use(async (c, next) => {
    var path = c.Request.Path;
    var isPageRequest = !path.StartsWithSegments("/api") && !path.StartsWithSegments("/_blazor") && !path.StartsWithSegments("/_framework") && !Path.HasExtension(path.Value);
    if (!isPageRequest) { await next(); return; }
    c.Response.Headers.CacheControl = "no-store";
    var isPasswordSetupPath = path.StartsWithSegments("/Change-Password") || path.StartsWithSegments("/Logout");
    if (!isPasswordSetupPath && c.User.HasClaim("must_change_password", "true")) {
        c.Response.Redirect("/Change-Password?returnUrl=" + Uri.EscapeDataString(path + c.Request.QueryString));
        return;
    }
    await next();
});
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = c => {
    if (c.File.Name.EndsWith(".html") || c.File.Name.EndsWith(".js")) c.Context.Response.Headers.CacheControl = "no-store";
} });
app.Use(async (c, next) => {
    if (c.Request.Path.StartsWithSegments("/api") && c.User.HasClaim("must_change_password", "true") && c.Request.Path != "/api/auth/change-password" && c.Request.Path != "/api/auth/logout") {
        c.Response.StatusCode = 403; await c.Response.WriteAsJsonAsync(new { error = "Change your temporary password first.", code = "password_change_required" }); return;
    }
    await next();
});
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
Accounts.Map(app);
AccountRoutes.Map(app);
app.MapGet("/api/config", () => new { categories = Catalog.Categories, provider = AiSettings.Provider(builder.Configuration), model = AiSettings.Model(builder.Configuration), configured = AiSettings.Configured(builder.Configuration) });
var api = app.MapGroup("/api").RequireAuthorization();
ImportedBank.Map(api);
PracticeProgress.Map(api);
Multiplayer.Map(api);
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

api.MapGet("/sets", async (PracticeService practice, HttpContext c) => await practice.GetSavedSets(UserId(c)));
api.MapPost("/sets", async (GenerateRequest r, PracticeService practice, HttpContext c) =>
    (await practice.CreateSet(UserId(c), r, c.RequestAborted)).ToHttp(id => new { id }));
api.MapGet("/sets/{id:guid}", async (Guid id, PracticeService practice, HttpContext c) =>
    (await practice.GetSet(UserId(c), id)).ToHttp());
api.MapPost("/sets/{id:guid}/save", async (Guid id, SaveRequest r, PracticeService practice, HttpContext c) =>
    (await practice.SaveTitle(UserId(c), id, r.Title)).ToHttp());
api.MapPost("/sets/{id:guid}/answer", async (Guid id, AnswerRequest r, PracticeService practice, HttpContext c) =>
    (await practice.CheckAnswer(UserId(c), id, r.Index, r.Choice)).ToHttp());
app.Run();

static string UserId(HttpContext c) => c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;

static string NormalizePostgresConnString(string conn) {
    if (conn.StartsWith("Host=", StringComparison.OrdinalIgnoreCase) ||
        conn.StartsWith("Server=", StringComparison.OrdinalIgnoreCase)) {
        return conn;
    }
    var match = System.Text.RegularExpressions.Regex.Match(conn, @"^postgres(?:ql)?://(?<user>[^:]+):(?<pass>.+)@(?<host>[^:/]+)(?::(?<port>\d+))?/(?<db>[^?]+)");
    if (match.Success) {
        var user = match.Groups["user"].Value;
        var pass = match.Groups["pass"].Value;
        if (pass.StartsWith("[") && pass.EndsWith("]")) {
            pass = pass[1..^1];
        }
        var host = match.Groups["host"].Value;
        var port = match.Groups["port"].Success ? match.Groups["port"].Value : "5432";
        var db = match.Groups["db"].Value;
        return $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
    }
    return conn;
}
