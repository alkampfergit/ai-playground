using Azure;
using Azure.AI.OpenAI;
using AzureAiLibrary.Configuration;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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

        if (endpoint == null)
        {
            throw new Exception("Error in configuration - no endpoint found for endpoint name: " + deployName);
        }

        OpenAIClient client = new OpenAIClient(
            new Uri(endpoint.BaseAddress),
            new AzureKeyCredential(Environment.GetEnvironmentVariable("AI_KEY")));

        var options = new ChatCompletionsOptions();
        foreach (var message in chatRequest.Messages)
        {
            options.Messages.Add(new ChatMessage(message.GetChatRole(), message.Content));
        }

        options.Temperature = (float?)chatRequest.Temperature;
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
        if (endpoint == null)
        {
            throw new Exception("Error in configuration - no endpoint found for endpoint name: " + httpClientName);
        }
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        var httpClient = _httpClientFactory.CreateClient(httpClientName);
        var response = await httpClient.SendAsync(request, token);

        if (token.IsCancellationRequested || !response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            //we could have rate limiting
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                TimeSpan sleep = TimeSpan.FromMinutes(1);
                if (response.Headers.RetryAfter?.Delta != null)
                {
                    sleep = response.Headers.RetryAfter.Delta.Value;
                }
                else
                {
                    var match = Regex.Match(error, "etry after\\s(\\d+)\\ssecond");
                    if (match.Success)
                    {
                        var seconds = int.Parse(match.Groups[1].Value);
                        sleep = TimeSpan.FromSeconds(seconds);
                    }
                }

                await Task.Delay(sleep, token);
                return await SendMessageAsync(httpClientName, chatRequest, token);
            }
            throw new Exception($"API call failed with status code: {response.StatusCode}: {response.ReasonPhrase} - {error}");
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        var chatResponse = JsonSerializer.Deserialize<ApiResponse>(responseBody)!;

        var message = chatResponse.Choices[0].Message;

        if (!String.IsNullOrEmpty(message.Content))
        {
            message.Content = Regex.Unescape(message.Content);
        }
        if (!String.IsNullOrEmpty(message.FunctionCall?.Arguments))
        {
            message.FunctionCall.Arguments = Regex.Unescape(message.FunctionCall.Arguments);
        }
        return message;
    }
}
