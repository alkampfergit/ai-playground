using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AzureAiLibrary;

public class Message
{
    public Message(string role, string content)
    {
        Role = role;
        Content = content;
    }

    [JsonConstructor]
    public Message()
    {
    }

    public static Message CreateSystemMessage(string message) 
    {
        return new Message("system", message);
    }

    public static Message CreateAssistantMessage(string message)
    {
        return new Message("assistant", message);
    }

    public static Message CreateUserMessage(string message)
    {
        return new Message("user", message);
    }

    [JsonPropertyName("role")]
	public string Role { get; set; }

	[JsonPropertyName("content")]
	public string Content { get; set; }
}

public class ApiPayload
{
	[JsonPropertyName("messages")]
	public List<Message> Messages { get; set; }

	[JsonPropertyName("temperature")]
	public double Temperature { get; set; }

	[JsonPropertyName("top_p")]
	public double TopP { get; set; }

	[JsonPropertyName("frequency_penalty")]
	public int FrequencyPenalty { get; set; }

	[JsonPropertyName("presence_penalty")]
	public int PresencePenalty { get; set; }

	[JsonPropertyName("max_tokens")]
	public int MaxTokens { get; set; }

	[JsonPropertyName("stop")]
	public string Stop { get; set; }
}

public class ApiResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("object")]
    public string Object { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("choices")]
    public List<Choice> Choices { get; set; }

    [JsonPropertyName("usage")]
    public Usage Usage { get; set; }
}

public class Choice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; set; }

    [JsonPropertyName("message")]
    public Message Message { get; set; }
}

public class Usage
{
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
