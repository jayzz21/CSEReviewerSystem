using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CseReviewer;
public class BankQuestion {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Owner { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string Category { get; set; } = "";
    public string Level { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string Source { get; set; } = "";
    public string Data { get; set; } = "";
}
public record BankCheckRequest(string Category, string Level, string Difficulty, int Count);
public record BankRequest(string Category, string Level, string Difficulty, int Count, bool AllowPartial = false);
public static class ImportedBank {
    public const string Schema = """
    IF OBJECT_ID(N'dbo.BankQuestions', N'U') IS NULL
    BEGIN
      CREATE TABLE dbo.BankQuestions (
        Id uniqueidentifier NOT NULL PRIMARY KEY, Owner nvarchar(64) NOT NULL,
        Fingerprint nvarchar(64) NOT NULL, Category nvarchar(64) NOT NULL,
        Level nvarchar(32) NOT NULL, Difficulty nvarchar(16) NOT NULL,
        Source nvarchar(200) NOT NULL, Data nvarchar(max) NOT NULL);
      CREATE UNIQUE INDEX IX_BankQuestions_Owner_Fingerprint ON dbo.BankQuestions(Owner,Fingerprint);
    END
    """;

    public static List<string> GetTargetCategories(string category, string level) =>
        category is "All 4 Subjects (Final Mock Exam)" or "All Subjects" or "Mixed subjects"
            ? (level == "Subprofessional"
                ? ["Numerical Ability", "Verbal Ability", "Clerical Ability", "General Information"]
                : ["Numerical Ability", "Verbal Ability", "Analytical Ability", "General Information"])
            : [category];

    public static Task<IQueryable<BankQuestion>> GetBaseQueryAsync(StudyDb db, string owner, string level, string difficulty) {
        var q = db.BankQuestions.AsQueryable();
        q = q.Where(x => x.Level == level || x.Level == "Both");
        if (difficulty != "Mixed") {
            q = q.Where(x => x.Difficulty == difficulty);
        }
        return Task.FromResult(q);
    }

    public static void Map(RouteGroupBuilder api) {
        api.MapGet("/bank", async (BankService bank) => await bank.GetBankSummary());
        api.MapPost("/imports/preview", async (ImportInput input, BankService bank, HttpContext c) =>
            (await bank.PreviewImport(UserId(c), input)).ToHttp()).RequireAuthorization("Admin");
        api.MapPost("/imports", async (ImportInput input, BankService bank, HttpContext c) =>
            (await bank.ImportQuestions(UserId(c), input)).ToHttp()).RequireAuthorization("Admin");
        api.MapPost("/bank/check", async (BankCheckRequest r, BankService bank) => (await bank.CheckBank(r)).ToHttp());
        api.MapPost("/bank/sets", async (BankRequest r, BankService bank, HttpContext c) => (await bank.CreateBankSet(UserId(c), r)).ToHttp());
    }

    private static string UserId(HttpContext c) => c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public static StudySet MakeSet(string owner, string title, List<ImportEntry> entries, string category, string level, string difficulty) {
        var questions = entries.Select(q => {
            var order = Enumerable.Range(0, 4).OrderBy(_ => Guid.NewGuid()).ToArray();
            return new Question(q.Prompt, order.Select(i => q.Options[i]).ToArray(), Array.IndexOf(order, q.Answer), q.Explanation, q.Steps);
        }).ToList();
        return new StudySet { Owner = owner, Title = title[..Math.Min(title.Length, 100)], Category = category, Level = level, Difficulty = difficulty, Source = "Imported", Saved = false, Count = questions.Count, Questions = JsonSerializer.Serialize(questions) };
    }
}
