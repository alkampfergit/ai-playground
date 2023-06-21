using AzureAiLibrary.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;

public class ChatClientTests
{
    private const string SuccessApiResponse = "{\"id\":\"chatcmpl-71BQ3DzZLzX1iJVEF6MBOJhYnYTU0\",\"object\":\"chat.completion\",\"created\":1680516579,\"model\":\"gpt-4\",\"choices\":[{\"index\":0,\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"\\\"The Lord of the Rings\\\" is a high-fantasy novel series written by J.R.R. Tolkien. It tells the story of the epic quest to destroy the One Ring, a powerful artifact created by the Dark Lord Sauron to dominate all of Middle-earth. The story follows the journey of the hobbit Frodo Baggins, who is entrusted with the task of carrying the Ring to Mount Doom, where it can be destroyed. Along the way, Frodo is joined by a diverse group of characters, known as the Fellowship of the Ring, who face various challenges and battles in order to protect Frodo and ensure the Ring's destruction. The series explores themes of friendship, courage, and the struggle between good and evil.\"}}],\"usage\":{\"completion_tokens\":148,\"prompt_tokens\":26,\"total_tokens\":174}}";
    private const string ErrorApiResponse = "{\"error\": \"API call failed\"}";

    [Fact]
    public async Task SendMessageAsync_ReturnsSuccess_WhenApiCallSucceeds()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(SuccessApiResponse),
            });

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://your-base-api-url.com/")
        };
        httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);
        var azureConfigMock = new Mock<IOptionsMonitor<AzureOpenAiConfiguration>>();

        var chatClient = new ChatClient(azureConfigMock.Object, httpClientFactoryMock.Object);

        var messages = new List<Message>
        {
            new Message { Role = "system", Content = "You are an AI assistant that helps people find information." },
            new Message { Role = "user", Content = "Tell me about The Lord of the Rings" }
        };

        var payload = new ApiPayload
        {
            Messages = messages,
            MaxTokens = 800,
            Temperature = 0.5,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            TopP = 0.95,
            Stop = null
        };

        // Act
        var response = await chatClient.SendMessageAsync("test", payload);

        // Assert
        Assert.Contains("The Lord of the Rings", response.Content);
    }

    [Fact]
    public async Task SendMessageAsync_ThrowsException_WhenApiCallFails()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent(ErrorApiResponse),
            });


        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://your-base-api-url.com/")
        };
        var azureConfigMock = new Mock<IOptionsMonitor<AzureOpenAiConfiguration>>();
        var chatClient = new ChatClient(azureConfigMock.Object, httpClientFactoryMock.Object);
        httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var messages = new List<Message>
        {
            new Message { Role = "system", Content = "You are an AI assistant that helps people find information." },
            new Message { Role = "user", Content = "Tell me about The Lord of the Rings" }
        };

        var payload = new ApiPayload
        {
            Messages = messages,
            MaxTokens = 800,
            Temperature = 0.5,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            TopP = 0.95,
            Stop = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () => await chatClient.SendMessageAsync("test", payload));
    }
}
