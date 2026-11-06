// See https://aka.ms/new-console-template for more information
using System.Net;
using System.Text.Json;
using CseReviewer;
using Microsoft.Extensions.Configuration;
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["AI:Endpoint"]="http://localhost:11434/api/generate", ["AI:Model"]="test-model"
}).Build();
var request = new GenerateRequest("Numerical Ability", "Professional", "Easy", 5, "Percentages", "ai");
var samples = Samples.Create(request);
async Task Check(string label, string response, bool valid, HttpStatusCode status = HttpStatusCode.OK) {
    var generator = new QuestionGenerator(new HttpClient(new FakeProvider(response, status)), config);
    try {
        var result = await generator.Generate(request, CancellationToken.None);
        if (!valid || result.Count != 5) throw new Exception("Unexpected accepted output: " + label);
    } catch (Exception ex) when (!valid && ex is InvalidDataException or JsonException or HttpRequestException) { }
    Console.WriteLine("PASS: " + label);
}
string Wrap(List<Question> qs) => JsonSerializer.Serialize(new { response = JsonSerializer.Serialize(new QuestionBatch(qs)) });
await Check("Accept valid provider output", Wrap(samples), true);
await Check("Reject missing response", "{}", false);
await Check("Reject malformed JSON", "not-json", false);
await Check("Reject unavailable model", "{}", false, HttpStatusCode.NotFound);
await Check("Reject missing questions", Wrap(samples.Take(4).ToList()), false);
var invalid = samples.ToList(); invalid[0] = invalid[0] with { Steps = [] };
await Check("Require numerical solving steps", Wrap(invalid), false);
invalid = samples.ToList(); invalid[0] = invalid[0] with { Answer = 9 };
await Check("Reject invalid answer index", Wrap(invalid), false);
invalid = samples.ToList(); invalid[0] = invalid[0] with { Options = ["A", "A", "B", "C"] };
await Check("Reject duplicate choices", Wrap(invalid), false);
invalid = samples.ToList(); invalid[1] = invalid[0];
await Check("Reject repeated questions", Wrap(invalid), false);
foreach (var (exception, status, code) in new (Exception, int, string)[] {
    (new HttpRequestException("Connection refused"), 503, "ai_unreachable"),
    (new HttpRequestException("Not found", null, HttpStatusCode.NotFound), 503, "ai_model_unavailable"),
    (new TaskCanceledException(), 504, "ai_timeout"),
    (new JsonException(), 502, "ai_invalid_output"),
    (new InvalidDataException(), 502, "ai_invalid_output"),
    (new HttpRequestException("Server error", null, HttpStatusCode.InternalServerError), 503, "ai_provider_error")
}) {
    var failure = GenerationFailure.From(exception, "llama3.2");
    if (failure.Status != status || failure.Code != code) throw new Exception("Incorrect failure classification");
    Console.WriteLine("PASS: distinguish " + code);
}
Console.WriteLine("All AI adapter checks passed (simulated provider; no live model called).");
class FakeProvider(string response, HttpStatusCode status) : HttpMessageHandler {
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        var body = await request.Content!.ReadAsStringAsync(ct);
        if (!body.Contains("Percentages") || !body.Contains("test-model")) throw new Exception("Missing model or learner focus in provider request");
        return new HttpResponseMessage(status) { Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json") };
    }
}
