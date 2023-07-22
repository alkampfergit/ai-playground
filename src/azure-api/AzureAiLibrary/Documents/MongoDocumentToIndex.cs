namespace AzureAiLibrary.Documents
{
    public class MongoDocumentToIndex
    {
        public required string Id { get; set; }

        public required IReadOnlyCollection<string> Pages { get; set; }

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Metadata { get; set; }

        /// <summary>
        /// Documents needed to be indexed into elastic
        /// </summary>
        public DateTime? Elastic { get; set; }
    }
}