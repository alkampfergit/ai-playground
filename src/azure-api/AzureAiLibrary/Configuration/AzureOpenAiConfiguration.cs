namespace AzureAiLibrary.Configuration
{
    public record Endpoint
    {
        public string Name { get; set; } = null!;
        public string Url { get; set; } = null!;
        public string BaseAddress { get; set; } = null!;
        
        /// <summary>
        /// It can be null, if it is null the key will be taken
        /// from the AI_KEY environment variable.
        /// </summary>
        public string ApiKey { get; set; } = null!;

        public string GetApiKey()
        {
            var apiKey = ApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = Environment.GetEnvironmentVariable("AI_KEY");
            }

            if (string.IsNullOrEmpty(apiKey)) throw new Exception("No API key found.");

            return apiKey;
        }
    }

    public record AzureOpenAiConfiguration
    {
        public string Default { get; set; } = null!;

        public List<Endpoint> Endpoints { get; set; } = null!;

        public Endpoint GetDefaultEndpoint()
        {
            return GetEndpoint(Default) ?? Endpoints[0];
        }

        public Endpoint? GetEndpoint(string endpointName)
        {
            return Endpoints
                .Find(e => e.Name.Equals(endpointName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
