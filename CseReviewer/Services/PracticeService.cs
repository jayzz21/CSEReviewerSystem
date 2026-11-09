using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CseReviewer;
public record StudySetSummary(Guid Id, string Title, string Category, string Level, string Difficulty, int Count, string Source, DateTime Created);
public record QuestionView(int Index, string Prompt, string[] Options);
public record AnswerFeedback(int Choice, bool Correct, int Answer, string Explanation, string[] Steps);
public record StudySetDetail(Guid Id, string Title, string Category, string Level, string Difficulty, string Source, bool Saved, List<AnswerFeedback> Progress, List<QuestionView> Questions);
public class PracticeService(IDbContextFactory<StudyDb> dbFactory, QuestionGenerator generator) {
    private const string _setNotFound = "Practice set not found.";

    public async Task<List<StudySetSummary>> GetSavedSets(string userId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Sets
            .Where(s => s.Owner == userId && s.Saved)
            .OrderByDescending(s => s.Created)
            .Select(s => new StudySetSummary(s.Id, s.Title, s.Category, s.Level, s.Difficulty, s.Count, s.Source, s.Created))
            .ToListAsync();
    }

    public async Task<ServiceResult<Guid>> CreateSet(string userId, GenerateRequest r, CancellationToken ct) {
        if (r.Mode == "ai") return ServiceResult<Guid>.Fail("AI custom questions are not available yet. Please choose Custom questions · Database.", 503, "ai_not_available");
        var error = Catalog.Validate(r);
        if (error != null) return ServiceResult<Guid>.Fail(error);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var set = new StudySet {
            Owner = userId,
            Title = $"{r.Category} practice",
            Category = r.Category,
            Focus = r.Focus,
            Level = r.Level,
            Difficulty = r.Difficulty,
            Source = r.Mode == "sample" ? "Sample" : "AI"
        };
        db.Sets.Add(set);
        await db.SaveChangesAsync(ct);
        var questions = await generator.Generate(r, ct, async partialQuestions => {
            set.Questions = JsonSerializer.Serialize(partialQuestions);
            set.Count = partialQuestions.Count;
            await db.SaveChangesAsync(ct);
        });
        set.Questions = JsonSerializer.Serialize(questions);
        set.Count = questions.Count;
        await db.SaveChangesAsync(ct);
        return ServiceResult<Guid>.Ok(set.Id);
    }

    public async Task<ServiceResult<StudySetDetail>> GetSet(string userId, Guid id) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Sets.SingleOrDefaultAsync(s => s.Id == id && s.Owner == userId);
        if (s == null) return ServiceResult<StudySetDetail>.Fail(_setNotFound, 404);
        var questions = JsonSerializer.Deserialize<List<Question>>(s.Questions)!.Select((q, i) => new QuestionView(i, q.Prompt, q.Options)).ToList();
        return ServiceResult<StudySetDetail>.Ok(new StudySetDetail(s.Id, s.Title, s.Category, s.Level, s.Difficulty, s.Source, s.Saved, PracticeProgress.Feedback(s), questions));
    }

    public async Task<ServiceResult> SaveTitle(string userId, Guid id, string? title) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Sets.SingleOrDefaultAsync(s => s.Id == id && s.Owner == userId);
        if (s == null) return ServiceResult.Fail(_setNotFound, 404);
        if (string.IsNullOrWhiteSpace(title) || title.Length > 100) return ServiceResult.Fail("Use a title between 1 and 100 characters.");
        s.Title = title.Trim();
        s.Saved = true;
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<AnswerFeedback>> CheckAnswer(string userId, Guid id, int index, int choice) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Sets.SingleOrDefaultAsync(s => s.Id == id && s.Owner == userId);
        if (s == null) return ServiceResult<AnswerFeedback>.Fail(_setNotFound, 404);
        var questions = JsonSerializer.Deserialize<List<Question>>(s.Questions)!;
        if (index < 0 || index >= questions.Count || choice < 0 || choice > 3) return ServiceResult<AnswerFeedback>.Fail("Invalid question or choice.");
        var q = questions[index];
        return ServiceResult<AnswerFeedback>.Ok(new AnswerFeedback(choice, q.Answer == choice, q.Answer, q.Explanation, q.Steps));
    }

    public async Task<ServiceResult> SaveProgress(string userId, Guid id, int[]? choices) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Sets.SingleOrDefaultAsync(s => s.Id == id && s.Owner == userId);
        if (s == null) return ServiceResult.Fail(_setNotFound, 404);
        if (choices == null || choices.Length >= s.Count || choices.Any(x => x < 0 || x > 3))
            return ServiceResult.Fail("Only unfinished practice progress can be saved. Choose valid answers.");
        s.Progress = JsonSerializer.Serialize(choices);
        s.Saved = true;
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DiscardProgress(string userId, Guid id) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Sets.SingleOrDefaultAsync(s => s.Id == id && s.Owner == userId);
        if (s == null) return ServiceResult.Fail(_setNotFound, 404);
        s.Saved = false;
        s.Progress = "[]";
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<Guid>> StartNextSet(string userId, Guid id, CancellationToken ct) {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var s = await db.Sets.SingleOrDefaultAsync(s => s.Id == id && s.Owner == userId, ct);
        if (s == null) return ServiceResult<Guid>.Fail(_setNotFound, 404);
        if (s.Source == "AI") return ServiceResult<Guid>.Fail("AI custom questions are not available yet. Create a new set using Custom questions · Database.", 503, "ai_not_available");
        StudySet next;
        if (s.Source == "Imported") {
            var query = db.BankQuestions.AsQueryable();
            if (s.Category != "Mixed subjects" && s.Category != "All 4 Subjects (Final Mock Exam)")
                query = query.Where(q => q.Category == s.Category && (q.Level == s.Level || q.Level == "Both"));
            var available = await query.CountAsync(ct);
            if (available == 0) return ServiceResult<Guid>.Fail("Import more matching questions before starting another set.");
            var takeCount = Math.Min(s.Count, available);
            var allRows = await query.ToListAsync(ct);
            var rows = allRows.OrderBy(_ => Guid.NewGuid()).Take(takeCount).ToList();
            next = ImportedBank.MakeSet(s.Owner, s.Title, rows.Select(q => JsonSerializer.Deserialize<ImportEntry>(q.Data)!).ToList(), s.Category, s.Level, s.Difficulty);
        } else {
            var request = new GenerateRequest(s.Category, s.Level, s.Difficulty, s.Count, s.Focus, "sample");
            var questions = await generator.Generate(request, ct);
            next = new StudySet { Owner = s.Owner, Title = s.Title, Category = s.Category, Level = s.Level, Difficulty = s.Difficulty, Source = s.Source, Focus = s.Focus, Count = questions.Count, Questions = JsonSerializer.Serialize(questions) };
        }
        s.Saved = false;
        s.Progress = "[]";
        db.Sets.Add(next);
        await db.SaveChangesAsync(ct);
        return ServiceResult<Guid>.Ok(next.Id);
    }
}
