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

    public static async Task<IQueryable<BankQuestion>> GetBaseQueryAsync(StudyDb db, string owner, string level, string difficulty) {
        var q = db.BankQuestions.AsQueryable();
        q = q.Where(x => x.Level == level || x.Level == "Both");
        if (await q.AnyAsync(x => x.Difficulty == difficulty)) {
            q = q.Where(x => x.Difficulty == difficulty);
        }
        return q;
    }

    public static void Map(RouteGroupBuilder api) {
        api.MapGet("/bank", async (StudyDb db, HttpContext c) => {
            var owner = c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var query = db.BankQuestions;
            return await query.GroupBy(q => new { q.Category, q.Level, q.Difficulty }).Select(g => new { g.Key.Category, g.Key.Level, g.Key.Difficulty, count = g.Count() }).ToListAsync();
        });
        api.MapPost("/imports/preview", async (ImportInput input, StudyDb db, HttpContext c) => {
            try {
                var parsed = HtmlImport.Parse(input);
                var owner = c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var existing = (await db.BankQuestions.Where(q => q.Owner == owner).Select(q => q.Fingerprint).ToListAsync()).ToHashSet();
                return Results.Ok(new { parsed.Title, valid = parsed.Questions.Count, newQuestions = parsed.Questions.Count(q => !existing.Contains(HtmlImport.Fingerprint(q))), parsed.Duplicates, parsed.Issues, categories = parsed.Questions.GroupBy(q => q.Category).Select(g => new { category = g.Key, count = g.Count() }) });
            } catch (ImportFormatException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization("Admin");
        api.MapPost("/imports", async (ImportInput input, StudyDb db, HttpContext c) => {
            ParsedImport parsed;
            try { parsed = HtmlImport.Parse(input); } catch (ImportFormatException ex) { return Results.BadRequest(new { error = ex.Message }); }
            if (parsed.Questions.Count == 0) return Results.BadRequest(new { error = "No valid questions to import." });
            var owner = c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await using var tx = await db.Database.BeginTransactionAsync();
            // Serialize commits for this local bank, including duplicate imports from separate tabs.
            if (db.Database.IsSqlServer()) {
                await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sp_getapplock @Resource='HusayImport', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 50001, 'Import is busy; retry shortly.', 1;");
            } else {
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('HusayImport'));");
            }
            var existing = (await db.BankQuestions.Where(q => q.Owner == owner).Select(q => q.Fingerprint).ToListAsync()).ToHashSet();
            var added = 0;
            foreach (var q in parsed.Questions) {
                var hash = HtmlImport.Fingerprint(q); if (!existing.Add(hash)) continue;
                db.BankQuestions.Add(new BankQuestion { Owner = owner, Fingerprint = hash, Category = q.Category, Level = q.Level, Difficulty = q.Difficulty, Source = input.FileName, Data = JsonSerializer.Serialize(q) }); added++;
            }
            var set = MakeSet(owner, parsed.Title + " · imported practice", parsed.Questions.OrderBy(_ => Guid.NewGuid()).Take(20).ToList(), "Mixed subjects", "Mixed practice", "Mixed");
            db.Sets.Add(set); await db.SaveChangesAsync(); await tx.CommitAsync();
            return Results.Ok(new { set.Id, added, totalQuestions = existing.Count, skipped = parsed.Issues.Count, duplicates = parsed.Duplicates + parsed.Questions.Count - added, count = set.Count });
        }).RequireAuthorization("Admin");
        api.MapPost("/bank/check", async (BankCheckRequest r, StudyDb db, HttpContext c) => {
            if (!Catalog.Categories.Contains(r.Category) || !new[] { "Professional", "Subprofessional" }.Contains(r.Level) || !new[] { "Easy", "Moderate", "Hard" }.Contains(r.Difficulty) || r.Count is < 1 or > 180) {
                return Results.BadRequest(new { error = "Choose valid subject, level, difficulty and 1–180 questions." });
            }
            if (r.Level == "Professional" && r.Category == "Clerical Ability" || r.Level == "Subprofessional" && r.Category == "Analytical Ability") {
                return Results.BadRequest(new { error = "This subject is not included in the selected exam level." });
            }
            var owner = c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var baseQuery = await GetBaseQueryAsync(db, owner, r.Level, r.Difficulty);
            var targetCats = GetTargetCategories(r.Category, r.Level);

            var breakdown = new List<object>();
            int totalAvailable = 0;
            foreach (var cat in targetCats) {
                int cnt = await baseQuery.Where(q => q.Category == cat).CountAsync();
                totalAvailable += cnt;
                breakdown.Add(new { category = cat, count = cnt });
            }

            return Results.Ok(new {
                requested = r.Count,
                available = totalAvailable,
                isEnough = totalAvailable >= r.Count,
                category = r.Category,
                breakdown
            });
        });
        api.MapPost("/bank/sets", async (BankRequest r, StudyDb db, HttpContext c) => {
            if (!Catalog.Categories.Contains(r.Category) || !new[] { "Professional", "Subprofessional" }.Contains(r.Level) || !new[] { "Easy", "Moderate", "Hard" }.Contains(r.Difficulty) || r.Count is < 1 or > 180 || r.Category == "Analytical Ability" && r.Level != "Professional" || r.Category == "Clerical Ability" && r.Level != "Subprofessional") {
                return Results.BadRequest(new { error = "Choose valid subject, level, difficulty and 1–180 questions." });
            }
            var owner = c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var baseQuery = await GetBaseQueryAsync(db, owner, r.Level, r.Difficulty);
            var targetCats = GetTargetCategories(r.Category, r.Level);

            var rowsByCat = new Dictionary<string, List<BankQuestion>>();
            int totalAvailable = 0;
            foreach (var cat in targetCats) {
                var matching = await baseQuery.Where(q => q.Category == cat).ToListAsync();
                rowsByCat[cat] = matching;
                totalAvailable += matching.Count;
            }

            if (totalAvailable == 0) {
                return Results.BadRequest(new { error = "No matching questions are available in the database for this subject and level. Import questions first." });
            }
            if (totalAvailable < r.Count && !r.AllowPartial) {
                return Results.BadRequest(new { error = $"Only {totalAvailable} matching questions are available in the database (requested {r.Count}).", available = totalAvailable });
            }

            int takeTotal = Math.Min(r.Count, totalAvailable);
            var selectedRows = new List<BankQuestion>();

            if (targetCats.Count > 1) {
                int quota = takeTotal / targetCats.Count;
                int rem = takeTotal % targetCats.Count;
                for (int i = 0; i < targetCats.Count; i++) {
                    var cat = targetCats[i];
                    int needed = quota + (i < rem ? 1 : 0);
                    var catList = rowsByCat[cat].OrderBy(_ => Guid.NewGuid()).Take(needed).ToList();
                    selectedRows.AddRange(catList);
                }
                if (selectedRows.Count < takeTotal) {
                    var pickedIds = selectedRows.Select(x => x.Id).ToHashSet();
                    var surplus = rowsByCat.Values.SelectMany(x => x).Where(x => !pickedIds.Contains(x.Id)).OrderBy(_ => Guid.NewGuid()).Take(takeTotal - selectedRows.Count);
                    selectedRows.AddRange(surplus);
                }
                selectedRows = selectedRows.OrderBy(_ => Guid.NewGuid()).ToList();
            } else {
                selectedRows = rowsByCat[targetCats[0]].OrderBy(_ => Guid.NewGuid()).Take(takeTotal).ToList();
            }

            var title = r.Category == "All 4 Subjects (Final Mock Exam)"
                ? $"Final Mock Exam · {selectedRows.Count} questions"
                : (takeTotal >= 100 ? $"{r.Category} · {selectedRows.Count} questions (Final Test)" : $"{r.Category} · imported bank");

            var set = MakeSet(owner, title, selectedRows.Select(q => JsonSerializer.Deserialize<ImportEntry>(q.Data)!).ToList(), r.Category, r.Level, r.Difficulty);
            db.Sets.Add(set);
            await db.SaveChangesAsync();
            return Results.Ok(new { set.Id, count = set.Count });
        });
    }
    public static StudySet MakeSet(string owner, string title, List<ImportEntry> entries, string category, string level, string difficulty) {
        var questions = entries.Select(q => {
            var order = Enumerable.Range(0, 4).OrderBy(_ => Guid.NewGuid()).ToArray();
            return new Question(q.Prompt, order.Select(i => q.Options[i]).ToArray(), Array.IndexOf(order, q.Answer), q.Explanation, q.Steps);
        }).ToList();
        return new StudySet { Owner = owner, Title = title[..Math.Min(title.Length, 100)], Category = category, Level = level, Difficulty = difficulty, Source = "Imported", Saved = false, Count = questions.Count, Questions = JsonSerializer.Serialize(questions) };
    }
}
