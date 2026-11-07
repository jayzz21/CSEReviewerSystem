using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using CseReviewer;

var builder = WebApplication.CreateBuilder(args);
var connStr = builder.Configuration.GetConnectionString("Study") ?? "";
bool isPostgres = connStr.Contains("supabase", StringComparison.OrdinalIgnoreCase) ||
                  connStr.Contains("postgres", StringComparison.OrdinalIgnoreCase) ||
                  connStr.StartsWith("Host=", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<StudyDb>(o => {
    if (isPostgres) {
        o.UseNpgsql(NormalizePostgresConnString(connStr));
    } else {
        o.UseSqlServer(connStr);
    }
});
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
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
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
    if (ctx.Request.Method == "POST" && !ctx.Request.HasJsonContentType()) { ctx.Response.StatusCode = 415; return; }
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
    var path = c.Request.Path.Value;
    if (path == "/" || string.Equals(path, "/index.html", StringComparison.OrdinalIgnoreCase) || string.Equals(path, "/admin.html", StringComparison.OrdinalIgnoreCase)) {
        c.Response.Headers.CacheControl = "no-store";
        var lobbyCode = c.Request.Query["code"].ToString();
        var lobbyReturn = c.Request.Query["view"] == "multiplayer" && lobbyCode.Length == 10 && lobbyCode.All(Uri.IsHexDigit)
            ? "lobby=" + lobbyCode.ToUpperInvariant() : "";
        if (c.User.Identity?.IsAuthenticated != true) {
            c.Response.Redirect("/login.html" + (lobbyReturn.Length > 0 ? "?" + lobbyReturn : "")); return;
        }
        if (c.User.HasClaim("must_change_password", "true")) {
            c.Response.Redirect("/login.html?changePassword=1" + (lobbyReturn.Length > 0 ? "&" + lobbyReturn : "")); return;
        }
        if (string.Equals(path, "/admin.html", StringComparison.OrdinalIgnoreCase) && !c.User.IsInRole("Admin") && !c.User.IsInRole("SuperAdmin")) {
            c.Response.Redirect("/"); return;
        }
    }
    await next();
});
app.UseDefaultFiles();
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
Accounts.Map(app);
app.MapGet("/api/config", () => new { categories = Catalog.Categories, provider = AiSettings.Provider(builder.Configuration), model = AiSettings.Model(builder.Configuration), configured = AiSettings.Configured(builder.Configuration) });
var api = app.MapGroup("/api").RequireAuthorization();
ImportedBank.Map(api);
PracticeProgress.Map(api);
Multiplayer.Map(api);

api.MapGet("/sets", async (StudyDb db, HttpContext c) => await db.Sets.Where(s => s.Owner == c.User.FindFirstValue(ClaimTypes.NameIdentifier)! && s.Saved).OrderByDescending(s => s.Created).Select(s => new { s.Id, s.Title, s.Category, s.Level, s.Difficulty, s.Count, s.Source, s.Created }).ToListAsync());
api.MapPost("/sets", async (GenerateRequest r, StudyDb db, QuestionGenerator generator, HttpContext c) => {
    if (r.Mode == "ai") return Results.Json(new { error = "AI custom questions are not available yet. Please choose Custom questions · Database.", code = "ai_not_available" }, statusCode: 503);
    var error = Catalog.Validate(r);
    if (error != null) return Results.BadRequest(new { error });

    // For large AI sets the generator makes two Groq calls (chunks).
    // We pre-create the row so the first chunk is never lost if the second call fails.
    var owner = c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var set = new StudySet {
        Owner      = owner,
        Title      = $"{r.Category} practice",
        Category   = r.Category,
        Focus      = r.Focus,
        Level      = r.Level,
        Difficulty = r.Difficulty,
        Source     = r.Mode == "sample" ? "Sample" : "AI"
    };
    db.Sets.Add(set);
    await db.SaveChangesAsync();   // persist the shell immediately

    var questions = await generator.Generate(r, c.RequestAborted, async partialQuestions => {
        // Callback: save each completed chunk to the DB as generation progresses.
        set.Questions = JsonSerializer.Serialize(partialQuestions);
        set.Count     = partialQuestions.Count;
        await db.SaveChangesAsync();
    });

    // Final save with the complete question list.
    set.Questions = JsonSerializer.Serialize(questions);
    set.Count     = questions.Count;
    await db.SaveChangesAsync();

    return Results.Ok(new { set.Id });
});
api.MapGet("/sets/{id:guid}", async (Guid id, StudyDb db, HttpContext c) => {
    var s = await db.Sets.SingleOrDefaultAsync(s => s.Id == id && s.Owner == c.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    return s == null ? Results.NotFound() : Results.Ok(new { s.Id, s.Title, s.Category, s.Level, s.Difficulty, s.Source, s.Saved, progress = PracticeProgress.Feedback(s), questions = JsonSerializer.Deserialize<List<Question>>(s.Questions)!.Select((q, i) => new { index = i, q.Prompt, q.Options }) });
});
api.MapPost("/sets/{id:guid}/save", async (Guid id, SaveRequest r, StudyDb db, HttpContext c) => {
    var s = await db.Sets.SingleOrDefaultAsync(s => s.Id == id && s.Owner == c.User.FindFirstValue(ClaimTypes.NameIdentifier)!); if (s == null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Length > 100) return Results.BadRequest(new { error = "Use a title between 1 and 100 characters." });
    s.Title = r.Title.Trim(); s.Saved = true; await db.SaveChangesAsync(); return Results.Ok();
});
api.MapPost("/sets/{id:guid}/answer", async (Guid id, AnswerRequest r, StudyDb db, HttpContext c) => {
    var s = await db.Sets.SingleOrDefaultAsync(s => s.Id == id && s.Owner == c.User.FindFirstValue(ClaimTypes.NameIdentifier)!); if (s == null) return Results.NotFound();
    var qs = JsonSerializer.Deserialize<List<Question>>(s.Questions)!;
    if (r.Index < 0 || r.Index >= qs.Count || r.Choice < 0 || r.Choice > 3) return Results.BadRequest(new { error = "Invalid question or choice." });
    var q = qs[r.Index]; return Results.Ok(new { correct = q.Answer == r.Choice, q.Answer, q.Explanation, q.Steps });
});
app.Run();

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

