using System.Text;
using System.Text.Json;

namespace AzureAiLibrary;

public class ChatClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ChatClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Message> SendMessageAsync(ApiPayload chatRequest)
    {
        var requestBody = JsonSerializer.Serialize(chatRequest);

        var request = new HttpRequestMessage(HttpMethod.Post, "openai/deployments/Gpt4/chat/completions?api-version=2023-03-15-preview")
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
        var chatResponse = JsonSerializer.Deserialize<ApiResponse>(responseBody);

        return chatResponse.Choices.First().Message;
    }
}
