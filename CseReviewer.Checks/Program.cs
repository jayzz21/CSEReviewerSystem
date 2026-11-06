// See https://aka.ms/new-console-template for more information
using System.Net;
using System.Text.Json;
using CseReviewer;
using Microsoft.Extensions.Configuration;
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["AI:Provider"]="Ollama", ["AI:Ollama:Endpoint"]="http://localhost:11434/api/generate", ["AI:Ollama:Model"]="test-model"
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
    (new HttpRequestException("Server error", null, HttpStatusCode.InternalServerError), 503, "ai_temporarily_unavailable")
}) {
    var failure = GenerationFailure.From(exception, "llama3.2");
    if (failure.Status != status || failure.Code != code) throw new Exception("Incorrect failure classification");
    Console.WriteLine("PASS: distinguish " + code);
}
var groqConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["AI:Provider"]="Groq", ["AI:Groq:ApiKey"]="fake-test-key", ["AI:Groq:Model"]="test-model"
}).Build();
foreach (var count in new[] { 5, 20 }) {
    var groqRequest = request with { Count = count };
    var payload = JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "stop", message = new { content = JsonSerializer.Serialize(new QuestionBatch(Samples.Create(groqRequest))) } } } });
    var result = await new QuestionGenerator(new HttpClient(new GroqProvider(payload)), groqConfig).Generate(groqRequest, CancellationToken.None);
    if (result.Count != count) throw new Exception("Groq returned wrong question count");
    Console.WriteLine($"PASS: Groq request/auth/JSON response for {count} questions");
}
foreach (var payload in new[] { "{}", "{\"choices\":[]}", "{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"{}\"}}]}" }) {
    try {
        await new QuestionGenerator(new HttpClient(new GroqProvider(payload)), groqConfig).Generate(request, CancellationToken.None);
        throw new Exception("Accepted malformed Groq completion");
    } catch (InvalidDataException) { Console.WriteLine("PASS: reject incomplete Groq completion"); }
}
foreach (var (exception, code) in new (Exception, string)[] {
    (new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized), "ai_auth_failed"),
    (new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden), "ai_auth_failed"),
    (new HttpRequestException("Quota", null, HttpStatusCode.TooManyRequests), "ai_rate_limited"),
    (new AiConfigurationException("Key missing"), "ai_not_configured")
}) {
    var failure = GenerationFailure.From(exception, "test-model", "Groq");
    if (failure.Code != code || failure.Error.Contains("ollama")) throw new Exception("Wrong cloud error mapping");
    Console.WriteLine("PASS: " + code);
}
var missingKey = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["AI:Provider"]="Groq" }).Build();
try {
    await new QuestionGenerator(new HttpClient(new GroqProvider("{}")), missingKey).Generate(request, CancellationToken.None);
    throw new Exception("Accepted missing key");
} catch (AiConfigurationException) { Console.WriteLine("PASS: missing key rejected before network call"); }
var sampleResult = await new QuestionGenerator(new HttpClient(new GroqProvider("{}")), missingKey).Generate(request with { Mode = "sample" }, CancellationToken.None);
if (sampleResult.Count != 5) throw new Exception("Sample mode needs no API key");
Console.WriteLine("All AI adapter checks passed (simulated provider; no live model called).");
var validCompletion = JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "stop", message = new { content = JsonSerializer.Serialize(new QuestionBatch(samples)) } } } });
foreach (var status in new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests }) {
    var handler = new RetryProvider(status, validCompletion);
    try { await new QuestionGenerator(new HttpClient(handler), groqConfig).Generate(request, CancellationToken.None); }
    catch (HttpRequestException) when (status == HttpStatusCode.TooManyRequests) { }
    if (handler.Calls != (status == HttpStatusCode.TooManyRequests ? 1 : 2)) throw new Exception("Incorrect retry count");
    Console.WriteLine("PASS: bounded retry policy for " + status);
}
var strict = JsonSerializer.SerializeToElement(QuestionOutput.Format("openai/gpt-oss-20b"));
if (strict.GetProperty("type").GetString() != "json_schema" || !strict.GetProperty("json_schema").GetProperty("strict").GetBoolean()) throw new Exception("Strict output disabled");
Console.WriteLine("PASS: strict schema for default model");
class RetryProvider(HttpStatusCode firstStatus, string success) : HttpMessageHandler {
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        Calls++;
        return Task.FromResult(new HttpResponseMessage(Calls == 1 ? firstStatus : HttpStatusCode.OK) {
            Content = new StringContent(Calls == 1 ? "{\"error\":{\"code\":\"json_validate_failed\"}}" : success, System.Text.Encoding.UTF8, "application/json")
        });
    }
}
class GroqProvider(string response) : HttpMessageHandler {
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        if (request.RequestUri?.ToString() != "https://api.groq.com/openai/v1/chat/completions" || request.Headers.Authorization?.ToString() != "Bearer fake-test-key") throw new Exception("Incorrect Groq destination or authorization");
        var body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(ct));
        if (body.GetProperty("model").GetString() != "test-model" || body.GetProperty("response_format").GetProperty("type").GetString() != "json_object" || !body.GetProperty("messages")[1].GetProperty("content").GetString()!.Contains("Percentages")) throw new Exception("Invalid Groq request contract");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json") };
    }
}
class FakeProvider(string response, HttpStatusCode status) : HttpMessageHandler {
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        var body = await request.Content!.ReadAsStringAsync(ct);
        if (!body.Contains("Percentages") || !body.Contains("test-model")) throw new Exception("Missing model or learner focus in provider request");
        return new HttpResponseMessage(status) { Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json") };
    }
}
