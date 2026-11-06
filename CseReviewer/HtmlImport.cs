using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HtmlAgilityPack;
using Jint;

namespace CseReviewer;
public record ImportEntry(string Category, string Level, string Difficulty, string Prompt, string[] Options, int Answer, string Explanation, string[] Steps);
public record ImportDocument(int Version, string Title, List<ImportEntry> Questions);
public record ImportInput(string FileName, string Html);
public record ParsedImport(string Title, List<ImportEntry> Questions, List<string> Issues, int Duplicates);
public class ImportFormatException(string message) : Exception(message);
public static class HtmlImport {
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, MaxDepth = 20 };
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static string Fingerprint(ImportEntry q) => Hash(JsonSerializer.Serialize(new {
        q.Category, q.Level, q.Difficulty, Prompt = q.Prompt.Trim(),
        Options = q.Options.Select(x => x.Trim()).OrderBy(x => x, StringComparer.Ordinal), Answer = q.Options[q.Answer].Trim()
    }));
    public static ParsedImport Parse(ImportInput input) {
        if (input.Html == null || Encoding.UTF8.GetByteCount(input.Html) > 2_000_000 || string.IsNullOrWhiteSpace(input.FileName) || input.FileName.Length > 200 || !new[] { ".html", ".htm" }.Contains(Path.GetExtension(input.FileName).ToLowerInvariant()))
            throw new ImportFormatException("Choose an HTML file smaller than 2 MB.");
        var html = new HtmlDocument(); html.LoadHtml(input.Html);
        var data = html.DocumentNode.SelectSingleNode("//script[@id='cse-question-bank' and @type='application/json']");
        ImportDocument? document;
        if (data != null) {
            try { document = JsonSerializer.Deserialize<ImportDocument>(data.InnerHtml, Json); }
            catch (JsonException) { throw new ImportFormatException("The cse-question-bank block is not valid JSON. Use the downloadable template."); }
        } else {
            var scripts = html.DocumentNode.SelectNodes("//script");
            var script = scripts?.Count == 1 ? scripts[0].InnerHtml.Replace("\r\n", "\n").Trim() : "";
            // Only the exact supplied legacy generator is accepted. Never execute arbitrary uploaded JavaScript.
            if (Hash(script) != "4B9DB6BED1630F3375E7669A915B4AC51CD343FB2892FB2448C339C054811A65")
                throw new ImportFormatException("Unsupported HTML layout. Use the HTML template with a cse-question-bank JSON block. Arbitrary scripts are not executed.");
            var engine = new Engine(o => o.TimeoutInterval(TimeSpan.FromSeconds(5)).LimitMemory(64_000_000).MaxStatements(500_000));
            engine.Execute(script[..script.IndexOf("const setsDiv", StringComparison.Ordinal)]);
            var json = engine.Evaluate("""
            JSON.stringify({version:1,title:'2027 CSE Reviewer',questions:(()=>{
              const output=[];
              for(let s=0;s<3;s++) {const rng=mulberry32(202700+s*999);
                for(const cat of categories) for(let i=0;i<36;i++) {
                  const q=cat.fn(rng,s,i);
                  output.push({category:cat.name,level:cat.name==='Analytical Ability'?'Professional':cat.name==='Clerical Ability'?'Subprofessional':'Both',difficulty:'Easy',prompt:q.q,options:q.choices,answer:q.ans,explanation:q.exp,steps:cat.name==='Numerical Ability'?[q.exp]:[]});
                }
              } return output;
            })()})
            """).AsString();
            document = JsonSerializer.Deserialize<ImportDocument>(json, Json);
        }
        if (document == null || document.Version != 1 || document.Questions == null || document.Questions.Count is < 1 or > 2000 || string.IsNullOrWhiteSpace(document.Title) || document.Title.Length > 100)
            throw new ImportFormatException("Use version 1, a title of 1–100 characters, and 1–2,000 questions.");
        var valid = new List<ImportEntry>(); var issues = new List<string>(); var seen = new HashSet<string>(); var duplicates = 0;
        for (var i = 0; i < document.Questions.Count; i++) {
            var q = document.Questions[i];
            var error = Validate(q);
            if (error != null) { issues.Add($"Question {i + 1}: {error}"); continue; }
            if (!seen.Add(Fingerprint(q!))) { duplicates++; continue; }
            valid.Add(q!);
        }
        return new(document.Title, valid, issues, duplicates);
    }
    private static string? Validate(ImportEntry? q) {
        if (q == null || !Catalog.Categories.Contains(q.Category) || !new[] { "Professional", "Subprofessional", "Both" }.Contains(q.Level) || !new[] { "Easy", "Moderate", "Hard" }.Contains(q.Difficulty)) return "Invalid category, level, or difficulty.";
        if (q.Category == "Analytical Ability" && q.Level != "Professional" || q.Category == "Clerical Ability" && q.Level != "Subprofessional") return "Analytical is Professional only; Clerical is Subprofessional only.";
        if (string.IsNullOrWhiteSpace(q.Prompt) || q.Prompt.Length > 5000 || string.IsNullOrWhiteSpace(q.Explanation) || q.Explanation.Length > 10000) return "A question and explanation are required (maximum 5,000 / 10,000 characters).";
        if (q.Options == null || q.Options.Length != 4 || q.Options.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 2000) || q.Options.Select(s => s.Trim()).Distinct(StringComparer.Ordinal).Count() != 4 || q.Answer is < 0 or > 3) return "Provide four distinct choices and answer index 0–3.";
        if (q.Steps == null || q.Steps.Length > 30 || q.Steps.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 4000) || q.Category == "Numerical Ability" && q.Steps.Length == 0) return "Numerical questions require solving steps; at most 30 nonempty steps are allowed.";
        return null;
    }
}
