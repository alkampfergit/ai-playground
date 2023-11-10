
using System.Text;

namespace AzureAiLibrary.Helpers
{
    public class JarvisApiCaller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public JarvisApiCaller(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> CallApiAsync(string url, string dto)
        {
            var httpClient = _httpClientFactory.CreateClient("jarvis");
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(dto, Encoding.UTF8, "application/json")
            };

            var response = await httpClient.SendAsync(request, CancellationToken.None);

            return await response.Content.ReadAsStringAsync();
        }
    }
}
