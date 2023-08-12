namespace AzureAiLibrary.Documents
{
    /// <summary>
    /// This is a raw mongodb document that get indexed into a mongo database to be 
    /// further processed and moved inside a search engine,
    /// </summary>
    public class MongoRawDocument
    {
        public required string Id { get; init; }

        public required IReadOnlyCollection<string> Pages { get; init; }

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Metadata { get; init; } = null!;

        /// <summary>
        /// If this is different from null it means that the document still needs to be processed
        /// by the analyzer, that generally takes what was extracted by tika and then create 
        /// a real document that can be indexed.
        /// </summary>
        public DateTime? Analyze { get; init; }
    }
}
