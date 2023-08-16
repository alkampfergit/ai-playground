using AzureAiLibrary.Documents.Jobs;
using Nest;
using System.Collections;

namespace AzureAiLibrary.Documents
{
    public class MongoDocumentToIndex
    {
        public required string Id { get; set; }

        public required IList<DocumentPage> Pages { get; set; }

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Metadata { get; set; }

        #region Polling properties

        public bool Processing { get; set; }

        /// <summary>
        /// Documents needed to be indexed into elastic so the LastModification helps a poller
        /// to index the document when it is modified.
        /// </summary>
        public DateTime? IndexToElastic { get; set; }

        /// <summary>
        /// If different from null, it will clean the text with GPT35
        /// </summary>
        public DateTime? CleanWithGpt35 { get; set; }

        public string CleanWithGpt35Errors { get; set; } = "";

        public DateTime? Embedding { get; set; }

        public string? EmbeddingModel { get; set; }

        public string? EmbeddingModelKey { get; set; }

        #endregion
    }

    public class DocumentPage
    {
        public DocumentPage(int number,  bool removed, string originalContent)
        {
            Number = number;
            Removed = removed;
            OriginalContent = originalContent;
        }

        public int Number { get; set; }

        public string Content { get; set; } = null!;

        public bool Removed { get; set; }

        public string OriginalContent { get; set; } 

        /// <summary>
        /// cl100k_base is the algorithm used by GPT3.5 turbo to tokenize.
        /// </summary>
        public int Cl100kBaseTokens { get; set; }

        public Gpt35PageInformation? Gpt35PageInformation { get; set; }
    }
}