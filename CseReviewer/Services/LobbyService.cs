using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CseReviewer;
public record LobbyPlayerView(string Id, string Name, bool Ready, int Answered, int? Score);
public record LobbyQuestionView(string Category, string Prompt, string[] Options);
public record LobbyBreakdownItem(string Category, int Count);
public record LobbyReview(LobbyQuestion Question, RoundResult Result);
public record LobbyResultItem(int Index, string Category, Question Question, int? Choice, string? RoundWinner);
public class LobbyView {
    public string Code { get; init; } = "";
    public int Game { get; init; }
    public string Status { get; init; } = "";
    public string Mode { get; init; } = "";
    public string Category { get; init; } = "";
    public string Level { get; init; } = "";
    public int Minutes { get; init; }
    public int Count { get; init; }
    public string Host { get; init; } = "";
    public string Me { get; init; } = "";
    public bool Ready { get; init; }
    public DateTime ServerTime { get; init; }
    public DateTime? Deadline { get; init; }
    public DateTime? NextRoundAt { get; init; }
    public List<LobbyPlayerView> Players { get; init; } = [];
    public string[] Winners { get; init; } = [];
    public int Index { get; init; }
    public bool Answered { get; init; }
    public LobbyQuestionView? Question { get; init; }
    public List<LobbyBreakdownItem> Breakdown { get; init; } = [];
    public LobbyReview? Review { get; init; }
    public List<LobbyResultItem>? Results { get; init; }
}
public class LobbyService(IDbContextFactory<StudyDb> dbFactory, LobbyNotifier notifier) {
    private const string _lobbyNotFound = "Lobby not found. Check the code.";
    private const int _maxPlayers = 20;

    private record ActionOutcome(bool IsForbidden = false, string Error = "") {
        public static readonly ActionOutcome Accepted = new();
        public static readonly ActionOutcome Forbidden = new(true);
        public static ActionOutcome Reject(string error) => new(false, error);
    }

    public async Task<ServiceResult<string>> Create(string userId, string userName, CreateLobby r) {
        if (r.Mode is not ("test" or "speed") || r.Count is < 30 or > 180 || r.Minutes < 1 || r.Level is not ("Professional" or "Subprofessional") || !Catalog.Categories.Contains(r.Category) || (r.Category == "Clerical Ability" && r.Level != "Subprofessional") || (r.Category == "Analytical Ability" && r.Level != "Professional"))
            return ServiceResult<string>.Fail("Choose a valid category, level, mode, 30–180 questions and a positive whole number of minutes.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var categories = ImportedBank.GetTargetCategories(r.Category, r.Level);
        var rows = await db.BankQuestions.AsNoTracking().Where(q => categories.Contains(q.Category) && (q.Level == r.Level || q.Level == "Both")).ToListAsync();
        var questions = new List<LobbyQuestion>();
        for (var i = 0; i < categories.Count; i++) {
            var needed = r.Count / categories.Count + (i < r.Count % categories.Count ? 1 : 0);
            var candidates = rows.Where(q => q.Category == categories[i]).Select(q => JsonSerializer.Deserialize<ImportEntry>(q.Data)!).GroupBy(q => q.Prompt.Trim().ToUpperInvariant()).Select(g => g.First()).OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();
            if (candidates.Count < needed) return ServiceResult<string>.Fail($"{categories[i]} needs {needed} unique questions, but only {candidates.Count} are available. Choose fewer questions or ask an administrator to add more.");
            foreach (var q in candidates.Take(needed)) questions.Add(ShuffleOptions(q.Category, q.Prompt, q.Options, q.Answer, q.Explanation, q.Steps));
        }
        var state = new LobbyState { Host = userId, Category = r.Category, Level = r.Level, Mode = r.Mode, Minutes = r.Minutes, Questions = questions.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList(), Players = [new() { Id = userId, Name = userName, Ready = true }] };
        var lobby = new Lobby { Code = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)), Data = JsonSerializer.Serialize(state) };
        db.Lobbies.Add(lobby);
        await db.SaveChangesAsync();
        return ServiceResult<string>.Ok(lobby.Code);
    }

    public Task<ServiceResult<LobbyView>> Read(string code, string userId, string userName) => Run(code, userId, userName, "read");
    public Task<ServiceResult<LobbyView>> Join(string code, string userId, string userName) => Run(code, userId, userName, "join");
    public Task<ServiceResult<LobbyView>> SetReady(string code, string userId, string userName, bool ready) => Run(code, userId, userName, ready ? "ready" : "unready");
    public Task<ServiceResult<LobbyView>> Start(string code, string userId, string userName) => Run(code, userId, userName, "start");
    public Task<ServiceResult<LobbyView>> Replay(string code, string userId, string userName, int game) => Run(code, userId, userName, "replay", replayGame: game);
    public Task<ServiceResult<LobbyView>> Answer(string code, string userId, string userName, LobbyAction answer) => Run(code, userId, userName, "answer", answer);
    public Task<ServiceResult<LobbyView>> Leave(string code, string userId, string userName) => Run(code, userId, userName, "leave");
    public Task<ServiceResult<LobbyView>> Cancel(string code, string userId, string userName) => Run(code, userId, userName, "cancel");

    private async Task<ServiceResult<LobbyView>> Run(string code, string userId, string userName, string action, LobbyAction? answer = null, int? replayGame = null) {
        code = code.ToUpperInvariant();
        if (code.Length != 10 || code.Any(x => !Uri.IsHexDigit(x))) return ServiceResult<LobbyView>.Fail(_lobbyNotFound, 404);
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var lobby = await LoadLobbyLocked(db, code);
        if (lobby == null) return ServiceResult<LobbyView>.Fail(_lobbyNotFound, 404);
        var state = JsonSerializer.Deserialize<LobbyState>(lobby.Data)!;
        var now = DateTime.UtcNow;
        if (state.Status == "waiting" && now > (state.WaitingSince ?? state.Created).AddHours(24)) state.Status = "cancelled";
        Multiplayer.Tick(state, now);
        var me = state.Players.SingleOrDefault(p => p.Id == userId);
        if (me == null) {
            var joinFailure = TryJoin(state, userId, userName, action, out me);
            if (joinFailure != null) return joinFailure;
        }
        var outcome = ApplyAction(state, me!, action, now, answer, replayGame);
        if (outcome.IsForbidden) return ServiceResult<LobbyView>.Fail("", 403);
        var updated = JsonSerializer.Serialize(state);
        var changed = updated != lobby.Data;
        if (changed) {
            lobby.Data = updated;
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        if (changed) notifier.Notify(code);
        if (outcome.Error.Length > 0) return ServiceResult<LobbyView>.Fail(outcome.Error, 409);
        return ServiceResult<LobbyView>.Ok(BuildView(code, state, me!, now));
    }

    private static async Task<Lobby?> LoadLobbyLocked(StudyDb db, string code) {
        if (db.Database.IsSqlServer())
            return await db.Lobbies.FromSqlInterpolated($"SELECT * FROM dbo.Lobbies WITH (UPDLOCK, ROWLOCK) WHERE Code = {code}").SingleOrDefaultAsync();
        return (await db.Lobbies.FromSqlInterpolated($"SELECT * FROM \"Lobbies\" WHERE \"Code\" = {code} FOR UPDATE").ToListAsync()).SingleOrDefault();
    }

    private static ServiceResult<LobbyView>? TryJoin(LobbyState state, string userId, string userName, string action, out LobbyPlayer? me) {
        me = null;
        if (action != "join") return ServiceResult<LobbyView>.Fail("Join the lobby first.", 404);
        if (state.Status != "waiting") return ServiceResult<LobbyView>.Fail("This lobby has already started or closed.", 409);
        if (state.Players.Count >= _maxPlayers) return ServiceResult<LobbyView>.Fail($"This lobby is full ({_maxPlayers} players).", 409);
        me = new LobbyPlayer { Id = userId, Name = userName };
        state.Players.Add(me);
        return null;
    }

    private static ActionOutcome ApplyAction(LobbyState state, LobbyPlayer me, string action, DateTime now, LobbyAction? answer, int? replayGame) => action switch {
        "ready" => ApplyReady(state, me, true),
        "unready" => ApplyReady(state, me, false),
        "start" => ApplyStart(state, me, now),
        "cancel" => ApplyCancel(state, me),
        "replay" => ApplyReplay(state, me, now, replayGame),
        "leave" => ApplyLeave(state, me),
        "answer" => ApplyAnswer(state, me, answer, now),
        _ => ActionOutcome.Accepted
    };

    private static ActionOutcome ApplyReady(LobbyState state, LobbyPlayer me, bool ready) {
        if (state.Status != "waiting") return ActionOutcome.Reject("The match has already started.");
        me.Ready = ready;
        return ActionOutcome.Accepted;
    }

    private static ActionOutcome ApplyStart(LobbyState state, LobbyPlayer me, DateTime now) {
        if (state.Host != me.Id) return ActionOutcome.Forbidden;
        if (state.Status != "waiting" || state.Players.Count < 2 || state.Players.Any(p => !p.Ready)) return ActionOutcome.Reject("At least two players must be ready before starting.");
        state.Status = "playing";
        state.Deadline = now.AddMinutes(state.Minutes);
        return ActionOutcome.Accepted;
    }

    private static ActionOutcome ApplyCancel(LobbyState state, LobbyPlayer me) {
        if (state.Host != me.Id) return ActionOutcome.Forbidden;
        state.Status = "cancelled";
        state.Deadline = null;
        state.NextRoundAt = null;
        return ActionOutcome.Accepted;
    }

    private static ActionOutcome ApplyReplay(LobbyState state, LobbyPlayer me, DateTime now, int? replayGame) {
        if (state.Host != me.Id) return ActionOutcome.Forbidden;
        if (state.Status != "finished" || replayGame != state.Game) return ActionOutcome.Reject("Only the current finished match can return to the lobby.");
        state.Game++;
        state.Status = "waiting";
        state.WaitingSince = now;
        state.Deadline = null;
        state.NextRoundAt = null;
        state.Round = 0;
        state.Rounds.Clear();
        foreach (var player in state.Players) {
            player.Answers.Clear();
            player.Ready = player.Id == state.Host;
        }
        state.Questions = state.Questions
            .Select(q => ShuffleOptions(q.Category, q.Question.Prompt, q.Question.Options, q.Question.Answer, q.Question.Explanation, q.Question.Steps))
            .OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))
            .ToList();
        return ActionOutcome.Accepted;
    }

    private static ActionOutcome ApplyLeave(LobbyState state, LobbyPlayer me) {
        if (state.Status == "playing") return ActionOutcome.Reject("A running match keeps your place. You may close the page and rejoin with this code.");
        if (state.Host == me.Id) state.Status = "cancelled";
        else state.Players.Remove(me);
        return ActionOutcome.Accepted;
    }

    private static ActionOutcome ApplyAnswer(LobbyState state, LobbyPlayer me, LobbyAction? answer, DateTime now) {
        if (state.Status != "playing" || state.NextRoundAt != null || answer == null || answer.Game != state.Game || answer.Index < 0 || answer.Index >= state.Questions.Count || answer.Choice is < 0 or > 3)
            return ActionOutcome.Reject("This question is no longer accepting answers.");
        var expected = state.Mode == "speed" ? state.Round : me.Answers.Count;
        if (answer.Index != expected || me.Answers.Any(a => a.Index == answer.Index))
            return ActionOutcome.Reject("This answer was already submitted or belongs to another question.");
        var correct = state.Questions[answer.Index].Question.Answer == answer.Choice;
        me.Answers.Add(new LobbyAnswer(answer.Index, answer.Choice, correct));
        if (state.Mode == "speed") {
            if (correct) Multiplayer.EndRound(state, me.Id, "First correct answer", now);
            else if (state.Players.All(p => p.Answers.Any(a => a.Index == state.Round))) Multiplayer.EndRound(state, null, "Everyone answered incorrectly", now);
        } else if (state.Players.All(p => p.Answers.Count == state.Questions.Count)) {
            state.Status = "finished";
        }
        return ActionOutcome.Accepted;
    }

    private static LobbyQuestion ShuffleOptions(string category, string prompt, string[] options, int answer, string explanation, string[] steps) {
        var order = Enumerable.Range(0, 4).OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray();
        return new LobbyQuestion(category, new Question(prompt, order.Select(i => options[i]).ToArray(), Array.IndexOf(order, answer), explanation, steps));
    }

    private static LobbyView BuildView(string code, LobbyState s, LobbyPlayer me, DateTime now) {
        var index = s.Mode == "speed" ? s.Round : me.Answers.Count;
        var question = s.Status == "playing" && index < s.Questions.Count ? s.Questions[index] : null;
        var scores = s.Players.Select(p => (Player: p, Score: s.Mode == "speed" ? s.Rounds.Count(r => r.Winner == p.Id) : p.Answers.Count(a => a.Correct))).ToArray();
        var maximum = scores.Max(x => x.Score);
        var winners = s.Status == "finished" && maximum > 0 ? scores.Where(x => x.Score == maximum).Select(x => x.Player.Id).ToArray() : [];
        var showScores = s.Mode == "speed" || s.Status == "finished";
        var reviewIndex = s.Mode == "speed" && s.NextRoundAt != null ? s.Round : -1;
        return new LobbyView {
            Code = code,
            Game = s.Game,
            Status = s.Status,
            Mode = s.Mode,
            Category = s.Category,
            Level = s.Level,
            Minutes = s.Minutes,
            Count = s.Questions.Count,
            Host = s.Host,
            Me = me.Id,
            Ready = me.Ready,
            ServerTime = now,
            Deadline = s.Deadline,
            NextRoundAt = s.NextRoundAt,
            Players = scores.Select(x => new LobbyPlayerView(x.Player.Id, x.Player.Name, x.Player.Ready, x.Player.Answers.Count, showScores ? x.Score : null)).ToList(),
            Winners = winners,
            Index = index,
            Answered = me.Answers.Any(a => a.Index == index),
            Question = question == null ? null : new LobbyQuestionView(question.Category, question.Question.Prompt, question.Question.Options),
            Breakdown = s.Questions.GroupBy(q => q.Category).Select(g => new LobbyBreakdownItem(g.Key, g.Count())).ToList(),
            Review = reviewIndex < 0 ? null : new LobbyReview(s.Questions[reviewIndex], s.Rounds.Last()),
            Results = s.Status != "finished" ? null : s.Questions.Select((q, i) => new LobbyResultItem(i, q.Category, q.Question, me.Answers.SingleOrDefault(a => a.Index == i)?.Choice, s.Rounds.SingleOrDefault(r => r.Index == i)?.Winner)).ToList()
        };
    }
}
