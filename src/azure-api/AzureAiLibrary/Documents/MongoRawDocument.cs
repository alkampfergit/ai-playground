namespace AzureAiLibrary.Documents
{
    /// <summary>
    /// This is a raw mongodb document that get indexed into a mongo database to be 
    /// further processed and moved inside a search engine,
    /// </summary>
    public class MongoRawDocument
    {
        public required string Id { get; set; }

        public required IReadOnlyCollection<string> Pages { get; set; }

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Metadata { get; set; }

        /// <summary>
        /// If this is different from null it means that the document still needs to be processed
        /// by the analyzer, that generally takes what was extracted by tika and then create 
        /// a real document that can be indexed.
        /// </summary>
        public DateTime? Analyze { get; set; }
    }
}
