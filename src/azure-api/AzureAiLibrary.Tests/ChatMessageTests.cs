using System.Text.Json;
using Xunit;

public class ApiPayloadTests
{
    [Fact]
    public void Serialize_ApiPayload_ShouldSerializeCorrectly()
    {
        var payload = new ApiPayload
        {
            Messages = new List<Message>
            {
                new Message { Role = "system", Content = "Test System Message" },
                new Message { Role = "user", Content = "Test User Message" }
            },
            MaxTokens = 100,
            Temperature = 0.8,
            FrequencyPenalty = 1,
            PresencePenalty = 2,
            TopP = 0.9,
            Stop = null
        };

        string jsonString = JsonSerializer.Serialize(payload);

        Assert.Contains("\"role\":\"system\"", jsonString);
        Assert.Contains("\"content\":\"Test System Message\"", jsonString);
        Assert.Contains("\"role\":\"user\"", jsonString);
        Assert.Contains("\"content\":\"Test User Message\"", jsonString);
        Assert.Contains("\"max_tokens\":100", jsonString);
        Assert.Contains("\"temperature\":0.8", jsonString);
        Assert.Contains("\"frequency_penalty\":1", jsonString);
        Assert.Contains("\"presence_penalty\":2", jsonString);
        Assert.Contains("\"top_p\":0.9", jsonString);
        Assert.Contains("\"stop\":null", jsonString);
    }

    [Fact]
    public void Deserialize_ApiPayload_ShouldDeserializeCorrectly()
    {
        string jsonString = @"{
            ""messages"": [
                { ""role"": ""system"", ""content"": ""Test System Message"" },
                { ""role"": ""user"", ""content"": ""Test User Message"" }
            ],
            ""max_tokens"": 100,
            ""temperature"": 0.8,
            ""frequency_penalty"": 1,
            ""presence_penalty"": 2,
            ""top_p"": 0.9,
            ""stop"": null
        }";

        ApiPayload payload = JsonSerializer.Deserialize<ApiPayload>(jsonString);

        Assert.NotNull(payload);
        Assert.NotNull(payload.Messages);
        Assert.Equal(2, payload.Messages.Count);
        Assert.Equal("system", payload.Messages[0].Role);
        Assert.Equal("Test System Message", payload.Messages[0].Content);
        Assert.Equal("user", payload.Messages[1].Role);
        Assert.Equal("Test User Message", payload.Messages[1].Content);
        Assert.Equal(100, payload.MaxTokens);
        Assert.Equal(0.8, payload.Temperature);
        Assert.Equal(1, payload.FrequencyPenalty);
        Assert.Equal(2, payload.PresencePenalty);
        Assert.Equal(0.9, payload.TopP);
        Assert.Null(payload.Stop);
    }
}
