using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AzureAiLibrary;

public class ApiPayload
{
    [JsonPropertyName("messages")] public List<Message> Messages { get; set; } = null!;

    [JsonPropertyName("temperature")] public double Temperature { get; set; }

    [JsonPropertyName("top_p")] public double TopP { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public int FrequencyPenalty { get; set; }

    [JsonPropertyName("presence_penalty")] public int PresencePenalty { get; set; }

    [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }

    [JsonPropertyName("stop")] public string? Stop { get; set; }
}

public class ApiResponse
{
    [JsonPropertyName("id")] public string Id { get; set; }  = null!;

    [JsonPropertyName("object")] public string Object { get; set; }  = null!;

    [JsonPropertyName("created")] public long Created { get; set; } 

    [JsonPropertyName("model")] public string Model { get; set; } = null!;

    [JsonPropertyName("choices")] public List<Choice> Choices { get; set; } = null!;

    [JsonPropertyName("usage")] public Usage Usage { get; set; } = null!;
}

public class Choice
{
    [JsonPropertyName("index")] public int Index { get; set; }

    [JsonPropertyName("finish_reason")] public string FinishReason { get; set; } = null!;

    [JsonPropertyName("message")] public Message Message { get; set; } = null!;
}

public class Usage
{
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; set; }

    [JsonPropertyName("total_tokens")] public int TotalTokens { get; set; }
}