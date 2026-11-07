using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CseReviewer;
public class Lobby {
    public string Code { get; set; } = "";
    public string Data { get; set; } = "";
}
public class LobbyState {
    public string Host { get; set; } = "";
    public string Mode { get; set; } = "test";
    public string Category { get; set; } = "";
    public string Level { get; set; } = "";
    public int Minutes { get; set; }
    public string Status { get; set; } = "waiting";
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime? Deadline { get; set; }
    public DateTime? NextRoundAt { get; set; }
    public int Round { get; set; }
    public List<LobbyQuestion> Questions { get; set; } = [];
    public List<LobbyPlayer> Players { get; set; } = [];
    public List<RoundResult> Rounds { get; set; } = [];
}
public record LobbyQuestion(string Category, Question Question);
public class LobbyPlayer {
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Ready { get; set; }
    public List<LobbyAnswer> Answers { get; set; } = [];
}
public record LobbyAnswer(int Index, int Choice, bool Correct);
public record RoundResult(int Index, string? Winner, string Reason);
public record CreateLobby(string Category, string Level, string Mode, int Count, int Minutes);
public record ReadyLobby(bool Ready);
public record LobbyAction(int Index, int Choice);
public static class Multiplayer {
    public const string Postgres = """
        CREATE TABLE IF NOT EXISTS "Lobbies" ("Code" varchar(10) PRIMARY KEY, "Data" text NOT NULL);
        ALTER TABLE "Lobbies" ENABLE ROW LEVEL SECURITY;
        """;
    public const string SqlServer = """
        IF OBJECT_ID(N'dbo.Lobbies', N'U') IS NULL
          CREATE TABLE dbo.Lobbies (Code nvarchar(10) NOT NULL PRIMARY KEY, Data nvarchar(max) NOT NULL);
        """;
    public static void Map(RouteGroupBuilder api) {
        api.MapPost("/lobbies", async (CreateLobby r, StudyDb db, HttpContext c) => {
            if (r.Mode is not ("test" or "speed") || r.Count is < 30 or > 180 || r.Minutes is < 1 or > 5 || r.Level is not ("Professional" or "Subprofessional") || !Catalog.Categories.Contains(r.Category) || (r.Category == "Clerical Ability" && r.Level != "Subprofessional") || (r.Category == "Analytical Ability" && r.Level != "Professional"))
                return Results.BadRequest(new { error = "Choose a valid category, level, mode, 30–180 questions and 1–5 minutes." });
            var categories = ImportedBank.GetTargetCategories(r.Category, r.Level);
            var rows = await db.BankQuestions.AsNoTracking().Where(q => categories.Contains(q.Category) && (q.Level == r.Level || q.Level == "Both")).ToListAsync();
            var questions = new List<LobbyQuestion>();
            for (var i = 0; i < categories.Count; i++) {
                var needed = r.Count / categories.Count + (i < r.Count % categories.Count ? 1 : 0);
                var candidates = rows.Where(q => q.Category == categories[i]).Select(q => JsonSerializer.Deserialize<ImportEntry>(q.Data)!).GroupBy(q => q.Prompt.Trim().ToUpperInvariant()).Select(g => g.First()).OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();
                if (candidates.Count < needed) return Results.BadRequest(new { error = $"{categories[i]} needs {needed} unique questions, but only {candidates.Count} are available. Choose fewer questions or ask an administrator to add more." });
                foreach (var q in candidates.Take(needed)) {
                    var order = Enumerable.Range(0, 4).OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray();
                    questions.Add(new(q.Category, new Question(q.Prompt, order.Select(x => q.Options[x]).ToArray(), Array.IndexOf(order, q.Answer), q.Explanation, q.Steps)));
                }
            }
            var state = new LobbyState { Host = User(c), Category = r.Category, Level = r.Level, Mode = r.Mode, Minutes = r.Minutes, Questions = questions.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList(), Players = [new() { Id = User(c), Name = c.User.Identity!.Name!, Ready = true }] };
            var lobby = new Lobby { Code = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)), Data = JsonSerializer.Serialize(state) };
            db.Lobbies.Add(lobby); await db.SaveChangesAsync();
            return Results.Ok(new { lobby.Code, path = "/multiplayer.html?code=" + lobby.Code });
        });
        api.MapGet("/lobbies/{code}", (string code, StudyDb db, HttpContext c) => Access(code, db, c, "read"));
        api.MapPost("/lobbies/{code}/join", (string code, StudyDb db, HttpContext c) => Access(code, db, c, "join"));
        api.MapPost("/lobbies/{code}/ready", (string code, ReadyLobby r, StudyDb db, HttpContext c) => Access(code, db, c, r.Ready ? "ready" : "unready"));
        api.MapPost("/lobbies/{code}/start", (string code, StudyDb db, HttpContext c) => Access(code, db, c, "start"));
        api.MapPost("/lobbies/{code}/answer", (string code, LobbyAction r, StudyDb db, HttpContext c) => Access(code, db, c, "answer", r));
        api.MapPost("/lobbies/{code}/leave", (string code, StudyDb db, HttpContext c) => Access(code, db, c, "leave"));
        api.MapPost("/lobbies/{code}/cancel", (string code, StudyDb db, HttpContext c) => Access(code, db, c, "cancel"));
    }
    private static string User(HttpContext c) => c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    // Row locks serialize answers across processes and deploys: one round has one winner.
    private static async Task<IResult> Access(string code, StudyDb db, HttpContext c, string action, LobbyAction? answer = null) {
        code = code.ToUpperInvariant();
        if (code.Length != 10 || code.Any(x => !Uri.IsHexDigit(x))) return Results.NotFound(new { error = "Lobby not found. Check the code." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        var lobby = db.Database.IsSqlServer()
            ? await db.Lobbies.FromSqlInterpolated($"SELECT * FROM dbo.Lobbies WITH (UPDLOCK, ROWLOCK) WHERE Code = {code}").SingleOrDefaultAsync()
            : (await db.Lobbies.FromSqlInterpolated($"SELECT * FROM \"Lobbies\" WHERE \"Code\" = {code} FOR UPDATE").ToListAsync()).SingleOrDefault();
        if (lobby == null) return Results.NotFound(new { error = "Lobby not found. Check the code." });
        var s = JsonSerializer.Deserialize<LobbyState>(lobby.Data)!;
        var me = s.Players.SingleOrDefault(p => p.Id == User(c));
        var now = DateTime.UtcNow;
        if (s.Status == "waiting" && now > s.Created.AddHours(24)) s.Status = "cancelled";
        Tick(s, now);
        string? error = null;
        if (me == null) {
            if (action != "join") return Results.NotFound(new { error = "Join the lobby first." });
            if (s.Status != "waiting") return Results.Conflict(new { error = "This lobby has already started or closed." });
            if (s.Players.Count >= 20) return Results.Conflict(new { error = "This lobby is full (20 players)." });
            me = new() { Id = User(c), Name = c.User.Identity!.Name! }; s.Players.Add(me);
        }
        switch (action) {
            case "ready": case "unready":
                if (s.Status != "waiting") error = "The match has already started.";
                else me.Ready = action == "ready";
                break;
            case "start":
                if (s.Host != me.Id) return Results.Forbid();
                if (s.Status != "waiting" || s.Players.Count < 2 || s.Players.Any(p => !p.Ready)) error = "At least two players must be ready before starting.";
                else { s.Status = "playing"; s.Deadline = now.AddMinutes(s.Minutes); }
                break;
            case "cancel":
                if (s.Host != me.Id) return Results.Forbid();
                if (s.Status is "waiting" or "playing") s.Status = "cancelled";
                break;
            case "leave":
                if (s.Status != "waiting") error = "A running match keeps your place. You may close the page and rejoin with this code.";
                else if (s.Host == me.Id) s.Status = "cancelled";
                else s.Players.Remove(me);
                break;
            case "answer":
                if (s.Status != "playing" || s.NextRoundAt != null || answer == null || answer.Index < 0 || answer.Index >= s.Questions.Count || answer.Choice is < 0 or > 3) { error = "This question is no longer accepting answers."; break; }
                var expected = s.Mode == "speed" ? s.Round : me.Answers.Count;
                if (answer.Index != expected || me.Answers.Any(a => a.Index == answer.Index)) { error = "This answer was already submitted or belongs to another question."; break; }
                var correct = s.Questions[answer.Index].Question.Answer == answer.Choice;
                me.Answers.Add(new(answer.Index, answer.Choice, correct));
                if (s.Mode == "speed") {
                    if (correct) EndRound(s, me.Id, "First correct answer", now);
                    else if (s.Players.All(p => p.Answers.Any(a => a.Index == s.Round))) EndRound(s, null, "Everyone answered incorrectly", now);
                } else if (s.Players.All(p => p.Answers.Count == s.Questions.Count)) s.Status = "finished";
                break;
        }
        var updated = JsonSerializer.Serialize(s);
        if (updated != lobby.Data) { lobby.Data = updated; await db.SaveChangesAsync(); }
        await transaction.CommitAsync();
        if (error != null) return Results.Conflict(new { error });
        c.Response.Headers.CacheControl = "no-store";
        return Results.Ok(View(code, s, me, now));
    }
    public static void Tick(LobbyState s, DateTime now) {
        if (s.Status != "playing") return;
        if (s.Mode == "test") { if (now >= s.Deadline) s.Status = "finished"; return; }
        while (s.Status == "playing") {
            if (s.NextRoundAt is DateTime next) {
                if (now < next) return;
                s.Round++; s.NextRoundAt = null;
                if (s.Round >= s.Questions.Count) { s.Status = "finished"; return; }
                s.Deadline = next.AddMinutes(s.Minutes);
            } else if (now >= s.Deadline) EndRound(s, null, "Time expired", s.Deadline!.Value);
            else return;
        }
    }
    private static void EndRound(LobbyState s, string? winner, string reason, DateTime at) {
        s.Rounds.Add(new(s.Round, winner, reason)); s.NextRoundAt = at.AddSeconds(5);
    }
    private static object View(string code, LobbyState s, LobbyPlayer me, DateTime now) {
        var index = s.Mode == "speed" ? s.Round : me.Answers.Count;
        var question = s.Status == "playing" && index < s.Questions.Count ? s.Questions[index] : null;
        var scores = s.Players.Select(p => new { p.Id, p.Name, p.Ready, answered = p.Answers.Count, score = s.Mode == "speed" ? s.Rounds.Count(r => r.Winner == p.Id) : p.Answers.Count(a => a.Correct) }).ToArray();
        var maximum = scores.Max(p => p.score);
        var winners = s.Status == "finished" && maximum > 0 ? scores.Where(p => p.score == maximum).Select(p => p.Id).ToArray() : [];
        var reviewIndex = s.Mode == "speed" && s.NextRoundAt != null ? s.Round : -1;
        return new {
            code, s.Status, s.Mode, s.Category, s.Level, s.Minutes, count = s.Questions.Count, s.Host,
            me = me.Id, ready = me.Ready, serverTime = now, s.Deadline, s.NextRoundAt,
            players = scores.Select(p => new { p.Id, p.Name, p.Ready, p.answered, score = s.Mode == "speed" || s.Status == "finished" ? (int?)p.score : null }),
            winners, index, answered = me.Answers.Any(a => a.Index == index),
            question = question == null ? null : new { question.Category, question.Question.Prompt, question.Question.Options },
            breakdown = s.Questions.GroupBy(q => q.Category).Select(g => new { category = g.Key, count = g.Count() }),
            review = reviewIndex < 0 ? null : new { question = s.Questions[reviewIndex], result = s.Rounds.Last() },
            results = s.Status != "finished" ? null : s.Questions.Select((q, i) => new { index = i, q.Category, q.Question, choice = me.Answers.SingleOrDefault(a => a.Index == i)?.Choice, roundWinner = s.Rounds.SingleOrDefault(r => r.Index == i)?.Winner })
        };
    }
}
