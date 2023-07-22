namespace AzureAiLibrary.Configuration
{
    public class DocumentsConfig
    {
        public string MongoUrl { get; set; } = null!;

        public string ElasticUrl { get; set; } = null!;
        public string Tika { get; set; } = null!;
        public string JavaBin { get; set; } = null!;
    }
}
