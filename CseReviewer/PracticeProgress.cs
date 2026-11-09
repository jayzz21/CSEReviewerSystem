using System.Security.Claims;
using System.Text.Json;

namespace CseReviewer;
public record ProgressRequest(int[] Choices);
public static class PracticeProgress {
    public const string Schema = """
        IF COL_LENGTH('dbo.Sets', 'Progress') IS NULL
          ALTER TABLE dbo.Sets ADD Progress nvarchar(max) NOT NULL DEFAULT '[]';
        IF COL_LENGTH('dbo.Sets', 'Focus') IS NULL
          ALTER TABLE dbo.Sets ADD Focus nvarchar(max) NOT NULL DEFAULT '';
        """;

    public static List<AnswerFeedback> Feedback(StudySet s) {
        var questions = JsonSerializer.Deserialize<List<Question>>(s.Questions)!;
        return JsonSerializer.Deserialize<int[]>(s.Progress)!.Select((choice, i) => {
            var q = questions[i];
            return new AnswerFeedback(choice, choice == q.Answer, q.Answer, q.Explanation, q.Steps);
        }).ToList();
    }

    public static void Map(RouteGroupBuilder api) {
        api.MapPost("/sets/{id:guid}/progress", async (Guid id, ProgressRequest r, PracticeService practice, HttpContext c) =>
            (await practice.SaveProgress(UserId(c), id, r.Choices)).ToHttp());
        api.MapPost("/sets/{id:guid}/discard", async (Guid id, PracticeService practice, HttpContext c) =>
            (await practice.DiscardProgress(UserId(c), id)).ToHttp());
        api.MapPost("/sets/{id:guid}/next", async (Guid id, PracticeService practice, HttpContext c) =>
            (await practice.StartNextSet(UserId(c), id, c.RequestAborted)).ToHttp(setId => new { id = setId }));
    }

    private static string UserId(HttpContext c) => c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
