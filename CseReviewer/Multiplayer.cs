using System.Security.Claims;

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
    public int Game { get; set; } = 1;
    public DateTime? WaitingSince { get; set; }
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
public record LobbyAction(int Index, int Choice, int Game = 1);
public record ReplayLobby(int Game);
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
        api.MapPost("/lobbies", async (CreateLobby r, LobbyService lobbies, HttpContext c) =>
            (await lobbies.Create(UserId(c), UserName(c), r)).ToHttp(code => new { code, path = "/Multiplayer?code=" + code }));
        api.MapGet("/lobbies/{code}", async (string code, LobbyService lobbies, HttpContext c) =>
            Respond(c, await lobbies.Read(code, UserId(c), UserName(c))));
        api.MapPost("/lobbies/{code}/join", async (string code, LobbyService lobbies, HttpContext c) =>
            Respond(c, await lobbies.Join(code, UserId(c), UserName(c))));
        api.MapPost("/lobbies/{code}/ready", async (string code, ReadyLobby r, LobbyService lobbies, HttpContext c) =>
            Respond(c, await lobbies.SetReady(code, UserId(c), UserName(c), r.Ready)));
        api.MapPost("/lobbies/{code}/start", async (string code, LobbyService lobbies, HttpContext c) =>
            Respond(c, await lobbies.Start(code, UserId(c), UserName(c))));
        api.MapPost("/lobbies/{code}/replay", async (string code, ReplayLobby r, LobbyService lobbies, HttpContext c) =>
            Respond(c, await lobbies.Replay(code, UserId(c), UserName(c), r.Game)));
        api.MapPost("/lobbies/{code}/answer", async (string code, LobbyAction r, LobbyService lobbies, HttpContext c) =>
            Respond(c, await lobbies.Answer(code, UserId(c), UserName(c), r)));
        api.MapPost("/lobbies/{code}/leave", async (string code, LobbyService lobbies, HttpContext c) =>
            Respond(c, await lobbies.Leave(code, UserId(c), UserName(c))));
        api.MapPost("/lobbies/{code}/cancel", async (string code, LobbyService lobbies, HttpContext c) =>
            Respond(c, await lobbies.Cancel(code, UserId(c), UserName(c))));
    }

    private static string UserId(HttpContext c) => c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static string UserName(HttpContext c) => c.User.Identity!.Name!;

    private static IResult Respond(HttpContext c, ServiceResult<LobbyView> result) {
        if (result.IsSuccess) c.Response.Headers.CacheControl = "no-store";
        return result.ToHttp();
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

    public static void EndRound(LobbyState s, string? winner, string reason, DateTime at) {
        s.Rounds.Add(new(s.Round, winner, reason));
        s.NextRoundAt = at.AddSeconds(5);
    }
}
