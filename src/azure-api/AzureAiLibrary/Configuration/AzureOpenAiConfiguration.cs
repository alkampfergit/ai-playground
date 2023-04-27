namespace AzureAiLibrary.Configuration
{
    public record Endpoint
    {
        public string Name { get; set; } = null!;
        public string Url { get; set; } = null!;
    }

    public record AzureOpenAiConfiguration
    {
        public string Default { get; set; } = null!;

        public List<Endpoint> Endpoints { get; set; } = null!;

        public Endpoint GetDefaultEndpoint()
        {
            return Endpoints
                .Find(e => e.Name.Equals(Default, StringComparison.OrdinalIgnoreCase)) ?? Endpoints[0];
        }
    }
}
