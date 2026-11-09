using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CseReviewer;
public record BankSummaryRow(string Category, string Level, string Difficulty, int Count);
public record CategoryCount(string Category, int Count);
public record ImportPreview(string Title, int Valid, int NewQuestions, int Duplicates, List<string> Issues, List<CategoryCount> Categories);
public record ImportSummary(Guid Id, int Added, int TotalQuestions, int Skipped, int Duplicates, int Count);
public record BankCheck(int Requested, int Available, bool IsEnough, string Category, List<CategoryCount> Breakdown);
public record BankSetCreated(Guid Id, int Count);
public class BankService(IDbContextFactory<StudyDb> dbFactory, AccountService accounts) {
    private static readonly string[] _levels = ["Professional", "Subprofessional"];
    private static readonly string[] _difficulties = ["Easy", "Moderate", "Hard", "Mixed"];

    private async Task<bool> IsAdmin(string userId) {
        var role = await accounts.GetRole(userId);
        return role is "Admin" or "SuperAdmin";
    }

    public async Task<List<BankSummaryRow>> GetBankSummary() {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.BankQuestions
            .GroupBy(q => new { q.Category, q.Level, q.Difficulty })
            .Select(g => new BankSummaryRow(g.Key.Category, g.Key.Level, g.Key.Difficulty, g.Count()))
            .ToListAsync();
    }

    public async Task<ServiceResult<ImportPreview>> PreviewImport(string userId, ImportInput input) {
        if (!await IsAdmin(userId)) return ServiceResult<ImportPreview>.Fail("", 403);
        ParsedImport parsed;
        try { parsed = HtmlImport.Parse(input); } catch (ImportFormatException ex) { return ServiceResult<ImportPreview>.Fail(ex.Message); }
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = (await db.BankQuestions.Where(q => q.Owner == userId).Select(q => q.Fingerprint).ToListAsync()).ToHashSet();
        var categories = parsed.Questions.GroupBy(q => q.Category).Select(g => new CategoryCount(g.Key, g.Count())).ToList();
        var newQuestions = parsed.Questions.Count(q => !existing.Contains(HtmlImport.Fingerprint(q)));
        return ServiceResult<ImportPreview>.Ok(new ImportPreview(parsed.Title, parsed.Questions.Count, newQuestions, parsed.Duplicates, parsed.Issues, categories));
    }

    public Task<ServiceResult<ImportSummary>> AddQuestion(string userId, ImportEntry entry) {
        var json = JsonSerializer.Serialize(new ImportDocument(1, "Admin question", [entry])).Replace("<", "\\u003c");
        var html = "<script type=\"application/json\" id=\"cse-question-bank\">" + json + "</script>";
        return ImportQuestions(userId, new ImportInput("admin-question.html", html));
    }

    public async Task<ServiceResult<ImportSummary>> ImportQuestions(string userId, ImportInput input) {
        if (!await IsAdmin(userId)) return ServiceResult<ImportSummary>.Fail("", 403);
        ParsedImport parsed;
        try { parsed = HtmlImport.Parse(input); } catch (ImportFormatException ex) { return ServiceResult<ImportSummary>.Fail(ex.Message); }
        if (parsed.Questions.Count == 0) return ServiceResult<ImportSummary>.Fail("No valid questions to import.");
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        if (db.Database.IsSqlServer()) {
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sp_getapplock @Resource='HusayImport', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 50001, 'Import is busy; retry shortly.', 1;");
        } else {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('HusayImport'));");
        }
        var existing = (await db.BankQuestions.Where(q => q.Owner == userId).Select(q => q.Fingerprint).ToListAsync()).ToHashSet();
        var added = 0;
        foreach (var q in parsed.Questions) {
            var hash = HtmlImport.Fingerprint(q);
            if (!existing.Add(hash)) continue;
            db.BankQuestions.Add(new BankQuestion { Owner = userId, Fingerprint = hash, Category = q.Category, Level = q.Level, Difficulty = q.Difficulty, Source = input.FileName, Data = JsonSerializer.Serialize(q) });
            added++;
        }
        var set = ImportedBank.MakeSet(userId, parsed.Title + " · imported practice", parsed.Questions.OrderBy(_ => Guid.NewGuid()).Take(20).ToList(), "Mixed subjects", "Mixed practice", "Mixed");
        db.Sets.Add(set);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult<ImportSummary>.Ok(new ImportSummary(set.Id, added, existing.Count, parsed.Issues.Count, parsed.Duplicates + parsed.Questions.Count - added, set.Count));
    }

    public async Task<ServiceResult<BankCheck>> CheckBank(BankCheckRequest r) {
        if (!Catalog.Categories.Contains(r.Category) || !_levels.Contains(r.Level) || !_difficulties.Contains(r.Difficulty) || r.Count < 1)
            return ServiceResult<BankCheck>.Fail("Choose valid subject, level, difficulty and a positive question count.");
        if (r.Level == "Professional" && r.Category == "Clerical Ability" || r.Level == "Subprofessional" && r.Category == "Analytical Ability")
            return ServiceResult<BankCheck>.Fail("This subject is not included in the selected exam level.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var baseQuery = await ImportedBank.GetBaseQueryAsync(db, "", r.Level, r.Difficulty);
        var breakdown = new List<CategoryCount>();
        var totalAvailable = 0;
        foreach (var category in ImportedBank.GetTargetCategories(r.Category, r.Level)) {
            var count = await baseQuery.Where(q => q.Category == category).CountAsync();
            totalAvailable += count;
            breakdown.Add(new CategoryCount(category, count));
        }
        return ServiceResult<BankCheck>.Ok(new BankCheck(r.Count, totalAvailable, totalAvailable >= r.Count, r.Category, breakdown));
    }

    public async Task<ServiceResult<BankSetCreated>> CreateBankSet(string userId, BankRequest r) {
        if (!Catalog.Categories.Contains(r.Category) || !_levels.Contains(r.Level) || !_difficulties.Contains(r.Difficulty) || r.Count < 1 || r.Category == "Analytical Ability" && r.Level != "Professional" || r.Category == "Clerical Ability" && r.Level != "Subprofessional")
            return ServiceResult<BankSetCreated>.Fail("Choose valid subject, level, difficulty and a positive question count.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var baseQuery = await ImportedBank.GetBaseQueryAsync(db, userId, r.Level, r.Difficulty);
        var targetCategories = ImportedBank.GetTargetCategories(r.Category, r.Level);
        var rowsByCategory = new Dictionary<string, List<BankQuestion>>();
        var totalAvailable = 0;
        foreach (var category in targetCategories) {
            var matching = await baseQuery.Where(q => q.Category == category).ToListAsync();
            rowsByCategory[category] = matching;
            totalAvailable += matching.Count;
        }
        if (totalAvailable == 0) return ServiceResult<BankSetCreated>.Fail("No matching questions are available in the database for this subject and level. Import questions first.");
        if (totalAvailable < r.Count && !r.AllowPartial) return ServiceResult<BankSetCreated>.Fail($"Only {totalAvailable} matching questions are available in the database (requested {r.Count}).");
        var takeTotal = Math.Min(r.Count, totalAvailable);
        var selectedRows = new List<BankQuestion>();
        if (targetCategories.Count > 1) {
            var quota = takeTotal / targetCategories.Count;
            var remainder = takeTotal % targetCategories.Count;
            for (var i = 0; i < targetCategories.Count; i++) {
                var needed = quota + (i < remainder ? 1 : 0);
                selectedRows.AddRange(rowsByCategory[targetCategories[i]].OrderBy(_ => Guid.NewGuid()).Take(needed));
            }
            if (selectedRows.Count < takeTotal) {
                var pickedIds = selectedRows.Select(x => x.Id).ToHashSet();
                var surplus = rowsByCategory.Values.SelectMany(x => x).Where(x => !pickedIds.Contains(x.Id)).OrderBy(_ => Guid.NewGuid()).Take(takeTotal - selectedRows.Count);
                selectedRows.AddRange(surplus);
            }
            selectedRows = selectedRows.OrderBy(_ => Guid.NewGuid()).ToList();
        } else {
            selectedRows = rowsByCategory[targetCategories[0]].OrderBy(_ => Guid.NewGuid()).Take(takeTotal).ToList();
        }
        var title = r.Category == "All 4 Subjects (Final Mock Exam)"
            ? $"Final Mock Exam · {selectedRows.Count} questions"
            : (takeTotal >= 100 ? $"{r.Category} · {selectedRows.Count} questions (Final Test)" : $"{r.Category} · imported bank");
        var set = ImportedBank.MakeSet(userId, title, selectedRows.Select(q => JsonSerializer.Deserialize<ImportEntry>(q.Data)!).ToList(), r.Category, r.Level, r.Difficulty);
        db.Sets.Add(set);
        await db.SaveChangesAsync();
        return ServiceResult<BankSetCreated>.Ok(new BankSetCreated(set.Id, set.Count));
    }
}
