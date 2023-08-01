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
        /// Documents needed to be indexed into elastic
        /// </summary>
        public DateTime? Elastic { get; set; }

        /// <summary>
        /// If different from null, it will clean the text with GPT35
        /// </summary>
        public DateTime? CleanWithGpt35 { get; set; }

        public string CleanWithGpt35Errors { get; set; } = "";  

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

        public string Content { get; set; }

        public bool Removed { get; set; }

        public string OriginalContent { get; set; }

        /// <summary>
        /// cl100k_base is the algorithm used by GPT3.5 turbo to tokenize.
        /// </summary>
        public int Cl100kBaseTokens { get; set; }

        public Gpt35PageInformation? Gpt35PageInformation { get; set; }

        public double[] BertEmbedding { get; set; }
    }
}