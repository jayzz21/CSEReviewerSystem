using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CseReviewer;
using Microsoft.EntityFrameworkCore;

public static class MultiplayerChecks {
    public static async Task Run(StudyDb db, HttpClient host, HttpClient guest, HttpClient outsider, Action<bool,string> check) {
        foreach (var category in new[] { "Numerical Ability", "Verbal Ability", "Analytical Ability", "General Information" })
            for (var i = 0; i < 45; i++) {
                var q = new ImportEntry(category, "Professional", "Easy", $"{category}: {i}+1?", ["correct", "wrong1", "wrong2", "wrong3"], 0, "Test explanation", ["Test solving step"]);
                db.BankQuestions.Add(new BankQuestion { Owner = "test-bank", Fingerprint = HtmlImport.Fingerprint(q), Category = category, Level = q.Level, Difficulty = q.Difficulty, Source = "tests", Data = JsonSerializer.Serialize(q) });
            }
        await db.SaveChangesAsync();
        async Task<JsonElement> State(HttpClient client, string code) => await client.GetFromJsonAsync<JsonElement>("/api/lobbies/" + code);
        async Task<string> Create(string mode, string category = "Numerical Ability", int count = 30) {
            var result = await host.PostAsJsonAsync("/api/lobbies", new { category, level = "Professional", mode, count, minutes = 1 });
            result.EnsureSuccessStatusCode(); return (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
        }
        async Task<LobbyState> Stored(string code) {
            var row = await db.Lobbies.AsNoTracking().SingleAsync(x => x.Code == code);
            return JsonSerializer.Deserialize<LobbyState>(row.Data)!;
        }
        async Task Expire(string code) {
            db.ChangeTracker.Clear(); var row = await db.Lobbies.SingleAsync(x => x.Code == code); var s = JsonSerializer.Deserialize<LobbyState>(row.Data)!;
            s.Deadline = DateTime.UtcNow.AddSeconds(-1); row.Data = JsonSerializer.Serialize(s); await db.SaveChangesAsync();
        }
        async Task Start(string code) {
            (await guest.PostAsJsonAsync($"/api/lobbies/{code}/join", new { })).EnsureSuccessStatusCode();
            (await guest.PostAsJsonAsync($"/api/lobbies/{code}/ready", new { ready = true })).EnsureSuccessStatusCode();
            (await host.PostAsJsonAsync($"/api/lobbies/{code}/start", new { })).EnsureSuccessStatusCode();
        }
        var invalid = await host.PostAsJsonAsync("/api/lobbies", new { category = "Numerical Ability", level = "Professional", mode = "speed", count = 29, minutes = 6 });
        check(invalid.StatusCode == HttpStatusCode.BadRequest, "Lobby enforces question and timer limits");
        var full = await Create("test", "All 4 Subjects (Final Mock Exam)", 180);
        check((await State(host, full)).GetProperty("breakdown").EnumerateArray().All(x => x.GetProperty("count").GetInt32() == 45), "Full 180-question exam covers four categories evenly");
        var code = await Create("test");
        var waiting = await State(host, code);
        check(waiting.GetProperty("question").ValueKind == JsonValueKind.Null, "Waiting lobby does not expose question bank");
        check((await outsider.GetAsync("/api/lobbies/" + code)).StatusCode == HttpStatusCode.NotFound, "Nonmembers cannot read lobby questions or players");
        check((await host.PostAsJsonAsync($"/api/lobbies/{code}/start", new { })).StatusCode == HttpStatusCode.Conflict, "Host cannot start alone");
        await guest.PostAsJsonAsync($"/api/lobbies/{code}/join", new { });
        await guest.PostAsJsonAsync($"/api/lobbies/{code}/join", new { });
        check((await State(host, code)).GetProperty("players").GetArrayLength() == 2, "Repeated join does not create duplicate players");
        check((await guest.PostAsJsonAsync($"/api/lobbies/{code}/start", new { })).StatusCode == HttpStatusCode.Forbidden, "Only host can start match");
        check((await host.PostAsJsonAsync($"/api/lobbies/{code}/start", new { })).StatusCode == HttpStatusCode.Conflict, "All players must be ready");
        await Start(code);
        var start = await State(host, code);
        check(!start.GetProperty("question").TryGetProperty("answer", out _) && start.GetProperty("results").ValueKind == JsonValueKind.Null, "Live match hides answer keys and review");
        check((await outsider.PostAsJsonAsync($"/api/lobbies/{code}/join", new { })).StatusCode == HttpStatusCode.Conflict, "Late joins are rejected");
        var stored = await Stored(code); var correct = stored.Questions[0].Question.Answer;
        (await host.PostAsJsonAsync($"/api/lobbies/{code}/answer", new { index = 0, choice = correct })).EnsureSuccessStatusCode();
        check((await host.PostAsJsonAsync($"/api/lobbies/{code}/answer", new { index = 0, choice = correct })).StatusCode == HttpStatusCode.Conflict, "Duplicate answers cannot increase scores");
        check((await State(host, code)).GetProperty("index").GetInt32() == 1, "Reconnecting resumes individual test position");
        await Expire(code);
        check((await guest.PostAsJsonAsync($"/api/lobbies/{code}/answer", new { index = 0, choice = correct })).StatusCode == HttpStatusCode.Conflict, "Server rejects late test answers");
        var ended = await State(host, code);
        check(ended.GetProperty("status").GetString() == "finished" && ended.GetProperty("winners").GetArrayLength() == 1 && ended.GetProperty("winners")[0].GetString() == "test-user-a", "Timed test declares highest correct score the winner");
        check(ended.GetProperty("results").GetArrayLength() == 30, "Completed test reveals explanations and solving steps");
        var lobbyCount = await db.Lobbies.CountAsync();
        check((await guest.PostAsJsonAsync($"/api/lobbies/{code}/replay", new { game = 1 })).StatusCode == HttpStatusCode.Forbidden, "Only host can reset a finished match");
        (await host.PostAsJsonAsync($"/api/lobbies/{code}/replay", new { game = 1 })).EnsureSuccessStatusCode();
        var replay = await State(guest, code);
        check(replay.GetProperty("status").GetString() == "waiting" && replay.GetProperty("game").GetInt32() == 2 && replay.GetProperty("players").GetArrayLength() == 2, "Replay returns all players to the same waiting room");
        check(await db.Lobbies.CountAsync() == lobbyCount, "Replay reuses the existing database row");
        var replayStored = await Stored(code);
        check(replayStored.Players.All(p => p.Answers.Count == 0) && !replay.GetProperty("ready").GetBoolean() && replayStored.Deadline == null && replayStored.NextRoundAt == null && replayStored.Rounds.Count == 0 && replayStored.Round == 0, "Replay clears answers, timers, round scores and guest readiness");
        check(replayStored.Questions.Select(q => q.Question.Prompt).Order().SequenceEqual(stored.Questions.Select(q => q.Question.Prompt).Order()) && replayStored.Questions.All(q => q.Question.Options[q.Question.Answer] == "correct"), "Replay preserves the question set and correct answers after reshuffling");
        check((await host.PostAsJsonAsync($"/api/lobbies/{code}/start", new { })).StatusCode == HttpStatusCode.Conflict, "Replay waits for guests to ready up again");
        check((await host.PostAsJsonAsync($"/api/lobbies/{code}/replay", new { game = 1 })).StatusCode == HttpStatusCode.Conflict, "Repeated replay requests cannot reset a waiting room");
        await Start(code);
        check((await host.PostAsJsonAsync($"/api/lobbies/{code}/answer", new { index = 0, choice = 0, game = 1 })).StatusCode == HttpStatusCode.Conflict, "Delayed answers from the previous game are rejected");
        (await host.PostAsJsonAsync($"/api/lobbies/{code}/answer", new { index = 0, choice = replayStored.Questions[0].Question.Answer, game = 2 })).EnsureSuccessStatusCode();
        await Expire(code); await State(host, code);
        check((await host.PostAsJsonAsync($"/api/lobbies/{code}/replay", new { game = 1 })).StatusCode == HttpStatusCode.Conflict, "Old replay requests cannot reset a later finished game");
        (await host.PostAsJsonAsync($"/api/lobbies/{code}/cancel", new { })).EnsureSuccessStatusCode();
        check((await State(guest, code)).GetProperty("status").GetString() == "cancelled", "Host can close a finished lobby for everyone");
        check((await host.PostAsJsonAsync($"/api/lobbies/{code}/replay", new { game = 2 })).StatusCode == HttpStatusCode.Conflict && (await outsider.PostAsJsonAsync($"/api/lobbies/{code}/join", new { })).StatusCode == HttpStatusCode.Conflict, "Closed lobbies cannot restart or accept new players");
        code = await Create("speed"); await Start(code); stored = await Stored(code); correct = stored.Questions[0].Question.Answer;
        var answers = await Task.WhenAll(host.PostAsJsonAsync($"/api/lobbies/{code}/answer", new { index = 0, choice = correct }), guest.PostAsJsonAsync($"/api/lobbies/{code}/answer", new { index = 0, choice = correct }));
        check(answers.Count(r => r.IsSuccessStatusCode) == 1, "Simultaneous correct answers produce exactly one round winner");
        check((await Stored(code)).Rounds.Count == 1, "Round outcome is persisted once");
        code = await Create("speed"); await Start(code); stored = await Stored(code); correct = stored.Questions[0].Question.Answer;
        await host.PostAsJsonAsync($"/api/lobbies/{code}/answer", new { index = 0, choice = (correct + 1) % 4 });
        check((await host.PostAsJsonAsync($"/api/lobbies/{code}/answer", new { index = 0, choice = correct })).StatusCode == HttpStatusCode.Conflict, "Wrong answer eliminates player for that round");
        await guest.PostAsJsonAsync($"/api/lobbies/{code}/answer", new { index = 0, choice = correct });
        check((await Stored(code)).Rounds[0].Winner == "test-user-b", "Remaining player can win speed round");
        var clockState = await Stored(code);
        Multiplayer.Tick(clockState, clockState.NextRoundAt!.Value.AddSeconds(1));
        check(clockState.Round == 1 && clockState.NextRoundAt == null && clockState.Status == "playing", "Next speed round starts after review with a new deadline");
        Multiplayer.Tick(clockState, DateTime.UtcNow.AddDays(1));
        check(clockState.Status == "finished", "Expired speed rounds finish even after all clients disconnect");
        check((await guest.PostAsJsonAsync($"/api/lobbies/{code}/cancel", new { })).StatusCode == HttpStatusCode.Forbidden, "Guest cannot cancel match");
    }
}
