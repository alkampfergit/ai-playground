using System.Collections.ObjectModel;

namespace AzureAiLibrary.Documents
{
    public static class VectorizationConstants
    {
        /// <summary>
        /// Constants used to queue embedding in python.
        /// </summary>
        public static IReadOnlyDictionary<string, string> Embeddings { get; private set; } =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Bert"] = "sentence-transformers/distiluse-base-multilingual-cased-v1",
                ["BertMpNetv2"] = "sentence-transformers/all-mpnet-base-v2"
            });
    }
}
