namespace CseReviewer;

public class AiConfigurationException(string message) : Exception(message);

public static class AiSettings {
    public static string Provider(IConfiguration config) => config["AI:Provider"] ?? "Groq";
    public static string? Key(IConfiguration config) => config["AI:Groq:ApiKey"] ?? config["GROQ_API_KEY"];
    public static string Model(IConfiguration config) => Provider(config) == "Ollama"
        ? config["AI:Ollama:Model"] ?? "llama3.2"
        : config["AI:Groq:Model"] ?? "openai/gpt-oss-20b";
    public static bool Configured(IConfiguration config) => Provider(config) switch {
        "Groq" => !string.IsNullOrWhiteSpace(Key(config)),
        "Ollama" => true,
        _ => false
    };
}
