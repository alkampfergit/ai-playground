using AzureAiLibrary.Configuration;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

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

    public async Task<Message> SendMessageAsync(ApiPayload chatRequest)
    {
        var requestBody = JsonSerializer.Serialize(chatRequest);
        var endpoint = _azureConfig.CurrentValue.GetDefaultEndpoint();
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        var _httpClient = _httpClientFactory.CreateClient("ChatClient");
        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"API call failed with status code {response.StatusCode}: {response.ReasonPhrase}");
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        var chatResponse = JsonSerializer.Deserialize<ApiResponse>(responseBody)!;

        return chatResponse.Choices[0].Message;
    }
}
