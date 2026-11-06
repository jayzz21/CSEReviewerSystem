using System.Net;
using System.Text.Json;

namespace CseReviewer;

public record GenerationFailure(int Status, string Code, string Error) {
    public static GenerationFailure From(Exception exception, string model, string provider = "Ollama") => exception switch {
        AiConfigurationException => new(503, "ai_not_configured", exception.Message),
        HttpRequestException { StatusCode: HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity } => new(502, "ai_request_rejected",
            $"{provider} rejected the generation settings. Check the configured model's output-format support. No set was saved."),
        HttpRequestException { StatusCode: HttpStatusCode.RequestEntityTooLarge } => new(502, "ai_request_too_large",
            $"{provider} rejected the request size. Try a smaller question set. No set was saved."),
        HttpRequestException { StatusCode: HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout } => new(503, "ai_temporarily_unavailable",
            $"{provider} is temporarily unable to generate this set, even after a retry. Please try again shortly. No set was saved."),
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } => new(503, "ai_auth_failed",
            $"{provider} rejected the API credentials or model access. Check the server's API key and your provider account permissions."),
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => new(429, "ai_rate_limited",
            $"{provider}'s request or token quota was reached. Wait for your quota to reset, then retry with 5 questions. Your saved sets are still available."),
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => new(503, "ai_model_unavailable",
            provider == "Ollama" ? $"Ollama could not find the model. Run 'ollama pull {model}' and check AI:Ollama:Endpoint." : $"{provider} could not find the configured model. Check AI:Groq:Model against the models available to your account."),
        HttpRequestException { StatusCode: null } => new(503, "ai_unreachable",
            provider == "Ollama" ? "Cannot connect to Ollama. Start 'ollama serve' and download the configured model with 'ollama pull " + model + "'." : $"Cannot connect to {provider}. Check your internet connection and retry."),
        OperationCanceledException => new(504, "ai_timeout",
            $"{provider} did not finish within five minutes. Try 5 questions first. No partial set was saved."),
        JsonException or InvalidDataException => new(502, "ai_invalid_output",
            $"{provider} returned an incomplete or invalid question set. No set was saved. Retry with 5 questions or use a more capable model."),
        _ => new(503, "ai_provider_error",
            $"{provider} returned an error. Check the configured model and provider availability, then retry. No set was saved.")
    };
}
