namespace AzureAiLibrary.Configuration
{
    public record Endpoint
    {
        public string Name { get; set; } = null!;
        
        /// <summary>
        /// To avoid to extract deployment name from the full
        /// url, Semantic Kernel and other libraries needs only
        /// base address and deployment name not the full url with
        /// OpenAI API version.
        /// </summary>
        public string DeploymentName { get; set; } = null!;
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
        
        /// <summary>
        /// This specify the configuration that is to be used for SK
        /// </summary>
        public string SemanticKernel { get; set; } = null!;

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

        public Endpoint GetSemanticKernelConfiguration() => GetEndpoint(SemanticKernel)!;
    }
}
