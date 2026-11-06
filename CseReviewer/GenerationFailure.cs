using System.Net;
using System.Text.Json;

namespace CseReviewer;

public record GenerationFailure(int Status, string Code, string Error) {
    public static GenerationFailure From(Exception exception, string model) => exception switch {
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => new(503, "ai_model_unavailable",
            $"Ollama could not find the configured model or API route. Run 'ollama pull {model}' and check AI:Endpoint in appsettings.json."),
        HttpRequestException { StatusCode: null } => new(503, "ai_unreachable",
            "Cannot connect to Ollama. Install Ollama from ollama.com, start it with 'ollama serve', and download the configured model with 'ollama pull " + model + "'. Then retry."),
        OperationCanceledException => new(504, "ai_timeout",
            "Ollama did not finish within five minutes. Try 5 questions first, or use a faster model. No partial set was saved."),
        JsonException or InvalidDataException => new(502, "ai_invalid_output",
            "Ollama returned an incomplete or invalid question set. No set was saved. Retry with 5 questions or use a more capable model."),
        _ => new(503, "ai_provider_error",
            "Ollama returned an error. Check its server logs and available memory, then retry. No set was saved.")
    };
}
