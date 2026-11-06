using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using CseReviewer;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<StudyDb>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Study")));
builder.Services.AddAuthentication("study").AddCookie("study", o => {
    o.Cookie.Name = "cse.study"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
    o.ExpireTimeSpan = TimeSpan.FromDays(180); o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddHttpClient<QuestionGenerator>(c => c.Timeout = TimeSpan.FromMinutes(5));
var app = builder.Build();
using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<StudyDb>().Database.EnsureCreatedAsync();
app.Use(async (ctx, next) => {
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    if (ctx.Request.Method == "POST" && !ctx.Request.HasJsonContentType()) { ctx.Response.StatusCode = 415; return; }
    try { await next(); }
    catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) {
        // The caller left the page; do not attempt to write to a closed connection.
    }
    catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidDataException or AiConfigurationException) {
        app.Logger.LogWarning(ex, "Question generation failed");
        if (!ctx.Response.HasStarted) {
            var failure = GenerationFailure.From(ex, AiSettings.Model(builder.Configuration), AiSettings.Provider(builder.Configuration));
            ctx.Response.StatusCode = failure.Status;
            await ctx.Response.WriteAsJsonAsync(new { error = failure.Error, code = failure.Code });
        }
    }
});
app.UseDefaultFiles(); app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization();
app.MapPost("/api/session", async (HttpContext ctx) => {
    if (ctx.User.Identity?.IsAuthenticated != true) await ctx.SignInAsync("study", new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "study")), new AuthenticationProperties { IsPersistent = true });
    return Results.Ok(new { profile = "This browser's study profile" });
});
app.MapGet("/api/config", () => new { categories = Catalog.Categories, provider = AiSettings.Provider(builder.Configuration), model = AiSettings.Model(builder.Configuration), configured = AiSettings.Configured(builder.Configuration) });
var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("/sets", async (StudyDb db, HttpContext c) => await db.Sets.Where(s => s.Owner == c.User.FindFirstValue(ClaimTypes.NameIdentifier)! && s.Saved).OrderByDescending(s => s.Created).Select(s => new { s.Id, s.Title, s.Category, s.Level, s.Difficulty, s.Count, s.Source, s.Created }).ToListAsync());
api.MapPost("/sets", async (GenerateRequest r, StudyDb db, QuestionGenerator generator, HttpContext c) => {
    var error = Catalog.Validate(r); if (error != null) return Results.BadRequest(new { error });
    var questions = await generator.Generate(r, c.RequestAborted);
    var set = new StudySet { Owner = c.User.FindFirstValue(ClaimTypes.NameIdentifier)!, Title = $"{r.Category} practice", Category = r.Category, Level = r.Level, Difficulty = r.Difficulty, Count = questions.Count, Source = r.Mode == "sample" ? "Sample" : "AI", Questions = JsonSerializer.Serialize(questions) };
    db.Sets.Add(set); await db.SaveChangesAsync(); return Results.Ok(new { set.Id });
});
api.MapGet("/sets/{id:guid}", async (Guid id, StudyDb db, HttpContext c) => {
    var s = await db.Sets.SingleOrDefaultAsync(s => s.Id == id && s.Owner == c.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    return s == null ? Results.NotFound() : Results.Ok(new { s.Id, s.Title, s.Category, s.Level, s.Difficulty, s.Source, s.Saved, questions = JsonSerializer.Deserialize<List<Question>>(s.Questions)!.Select((q, i) => new { index = i, q.Prompt, q.Options }) });
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

