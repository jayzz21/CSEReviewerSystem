using System.Text.Json;

namespace CseReviewer;

public static class QuestionOutput {
    // Strict decoding is supported by Groq's GPT-OSS models. Other models retain JSON mode.
    public static object Format(string model) => model is "openai/gpt-oss-20b" or "openai/gpt-oss-120b"
        ? new { type = "json_schema", json_schema = new { name = "practice_set", strict = true, schema = Schema } }
        : new { type = "json_object" };

    private static readonly JsonElement Schema = JsonSerializer.Deserialize<JsonElement>("""
    {"type":"object","additionalProperties":false,"required":["questions"],"properties":{
      "questions":{"type":"array","items":{"type":"object","additionalProperties":false,
        "required":["prompt","options","answer","explanation","steps"],"properties":{
          "prompt":{"type":"string"},"options":{"type":"array","items":{"type":"string"}},
          "answer":{"type":"integer"},"explanation":{"type":"string"},
          "steps":{"type":"array","items":{"type":"string"}}
        }}}
    }}
    """);
}

