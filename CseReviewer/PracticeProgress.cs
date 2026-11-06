using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CseReviewer;
public record ProgressRequest(int[] Choices);
public static class PracticeProgress {
    public const string Schema = """
        IF COL_LENGTH('dbo.Sets', 'Progress') IS NULL
          ALTER TABLE dbo.Sets ADD Progress nvarchar(max) NOT NULL DEFAULT '[]';
        IF COL_LENGTH('dbo.Sets', 'Focus') IS NULL
          ALTER TABLE dbo.Sets ADD Focus nvarchar(max) NOT NULL DEFAULT '';
        """;
    public static object[] Feedback(StudySet s) {
        var questions = JsonSerializer.Deserialize<List<Question>>(s.Questions)!;
        return JsonSerializer.Deserialize<int[]>(s.Progress)!.Select((choice, i) => {
            var q = questions[i];
            return (object)new { choice, correct = choice == q.Answer, q.Answer, q.Explanation, q.Steps };
        }).ToArray();
    }
    public static void Map(RouteGroupBuilder api) {
        api.MapPost("/sets/{id:guid}/progress", async (Guid id, ProgressRequest r, StudyDb db, HttpContext c) => {
            var s = await db.Sets.SingleOrDefaultAsync(s => s.Id == id && s.Owner == c.User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (s == null) return Results.NotFound();
            if (r.Choices == null || r.Choices.Length >= s.Count || r.Choices.Any(x => x < 0 || x > 3))
                return Results.BadRequest(new { error = "Only unfinished practice progress can be saved. Choose valid answers." });
            s.Progress = JsonSerializer.Serialize(r.Choices); s.Saved = true;
            await db.SaveChangesAsync(); return Results.Ok();
        });
        api.MapPost("/sets/{id:guid}/discard", async (Guid id, StudyDb db, HttpContext c) => {
            var s = await db.Sets.SingleOrDefaultAsync(s => s.Id == id && s.Owner == c.User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (s == null) return Results.NotFound();
            s.Saved = false; s.Progress = "[]";
            await db.SaveChangesAsync(); return Results.Ok();
        });
        api.MapPost("/sets/{id:guid}/next", async (Guid id, StudyDb db, QuestionGenerator generator, HttpContext c) => {
            var s = await db.Sets.SingleOrDefaultAsync(s => s.Id == id && s.Owner == c.User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (s == null) return Results.NotFound();
            StudySet next;
            if (s.Source == "AI") return Results.Json(new { error = "AI custom questions are not available yet. Create a new set using Custom questions · Database.", code = "ai_not_available" }, statusCode: 503);
            if (s.Source == "Imported") {
                var query = db.BankQuestions.AsQueryable();
                if (!await query.AnyAsync()) query = db.BankQuestions;
                if (s.Category != "Mixed subjects" && s.Category != "All 4 Subjects (Final Mock Exam)")
                    query = query.Where(q => q.Category == s.Category && (q.Level == s.Level || q.Level == "Both"));
                var available = await query.CountAsync();
                if (available == 0) return Results.BadRequest(new { error = "Import more matching questions before starting another set." });
                int takeCount = Math.Min(s.Count, available);
                var allRows = await query.ToListAsync();
                var rows = allRows.OrderBy(_ => Guid.NewGuid()).Take(takeCount).ToList();
                next = ImportedBank.MakeSet(s.Owner, s.Title, rows.Select(q => JsonSerializer.Deserialize<ImportEntry>(q.Data)!).ToList(), s.Category, s.Level, s.Difficulty);
            } else {
                var request = new GenerateRequest(s.Category, s.Level, s.Difficulty, s.Count, s.Focus, s.Source == "AI" ? "ai" : "sample");
                var questions = await generator.Generate(request, c.RequestAborted);
                next = new StudySet { Owner = s.Owner, Title = s.Title, Category = s.Category, Level = s.Level, Difficulty = s.Difficulty, Source = s.Source, Focus = s.Focus, Count = questions.Count, Questions = JsonSerializer.Serialize(questions) };
            }
            s.Saved = false; s.Progress = "[]";
            db.Sets.Add(next); await db.SaveChangesAsync(); return Results.Ok(new { next.Id });
        });
    }
}
