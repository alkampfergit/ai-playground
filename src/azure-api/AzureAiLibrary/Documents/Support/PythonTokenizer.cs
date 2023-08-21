using System.Collections;
using System.Net.Http.Json;
using System.Text.Json;

namespace AzureAiLibrary.Documents.Support
{
    public class PythonTokenizer
    {
        private readonly IHttpClientFactory _httpClientFactory;

        private static JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = true,

        };

        public PythonTokenizer(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<double[]> Tokenize(string text, string modelCode)
        {
            if (!VectorizationConstants.Embeddings.TryGetValue(modelCode, out var model))
            {
                throw new ArgumentException($"Model {modelCode} not found");
            }

            var data = new
            {
                Text = text,
                Model = model
            };

            var client = _httpClientFactory.CreateClient("python_embeddings");
            var response = await client.PostAsJsonAsync(
                "vectorize",
                data,
                _options);

            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<double>();
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
           
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse)!;
            var vector = ((JsonElement)result["vector"]).EnumerateArray().Select(x => x.GetDouble()).ToArray(); 
          
            return vector;
        }
    }
}
