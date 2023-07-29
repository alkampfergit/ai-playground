using AzureAiLibrary.Configuration;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;

namespace AzureAiLibrary;

public class ChatClient
{
    private readonly IOptionsMonitor<AzureOpenAiConfiguration> _azureConfig;
    private readonly IHttpClientFactory _httpClientFactory;

    public ChatClient(
        IOptionsMonitor<AzureOpenAiConfiguration> azureConfig,
        IHttpClientFactory httpClientFactory)
    {
        _azureConfig = azureConfig;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Message> SendMessageStreamingAsync(
        string deployName,
        ApiPayload chatRequest)
    {
        var endpoint = _azureConfig.CurrentValue.GetEndpoint(deployName);

        OpenAIClient client = new OpenAIClient(
            new Uri(endpoint.BaseAddress),
            new AzureKeyCredential(Environment.GetEnvironmentVariable("AI_KEY")));

        var options = new ChatCompletionsOptions();
        foreach (var message in chatRequest.Messages)
        {
            options.Messages.Add(new ChatMessage(message.GetChatRole(), message.Content));
        }

        options.Temperature = (float?) chatRequest.Temperature;
        options.MaxTokens = chatRequest.MaxTokens;
        options.FrequencyPenalty = chatRequest.PresencePenalty;
        options.PresencePenalty = chatRequest.PresencePenalty;
        
        // ### If streaming is selected
        Response<StreamingChatCompletions> response = await client.GetChatCompletionsStreamingAsync(
            deploymentOrModelName: endpoint.Name,
            options);
        
        StreamingChatCompletions streamingChatCompletions = response.Value;
        return new Message(streamingChatCompletions);
    }

    public Task<Message> SendMessageAsync(
        string httpClientName,
        ApiPayload chatRequest)
    {
        return SendMessageAsync(httpClientName, chatRequest, CancellationToken.None);
    }
        
    public async Task<Message> SendMessageAsync(
        string httpClientName,
        ApiPayload chatRequest,
        CancellationToken token)
    {
        var requestBody = JsonSerializer.Serialize(chatRequest);
        var endpoint = _azureConfig.CurrentValue.GetEndpoint(httpClientName);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        var httpClient = _httpClientFactory.CreateClient(httpClientName);
        var response = await httpClient.SendAsync(request, token);

        if (token.IsCancellationRequested || !response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"API call failed with status code {response.StatusCode}: {response.ReasonPhrase}");
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        var chatResponse = JsonSerializer.Deserialize<ApiResponse>(responseBody)!;

        return chatResponse.Choices[0].Message;
    }
}
