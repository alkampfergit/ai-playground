using System.Text.Json.Serialization;

namespace AzureAiLibrary.Documents.DocumentChat
{
    /// <summary>
    /// Special class to help working with segments using basic class <see cref="ElasticDocument"/>" 
    /// </summary>
    public class ElasticDocumentSegment : ElasticDocument
    {
        public ElasticDocumentSegment() : base(Guid.NewGuid().ToString())
        {
        }

        public ElasticDocumentSegment(
            string documentId,
            string content,
            int pageId) : this()
        {
            Content = content;
            DocumentId = documentId;
            PageId = pageId;
        }

        public string Content
        {
            get => base.GetTextProperty("content") ?? string.Empty;
            set => AddTextProperty("content", value);
        }

        /// <summary>
        /// This is the id of the document, is used to search for document.
        /// </summary>
        public string DocumentId
        {
            get => base.GetStringProperty("docid") ?? string.Empty;
            set => AddStringProperty("docid", value);
        }

        public int PageId
        {
            get => (int) (GetNumericProperty("pageId") ?? 0);
            set => AddNumericProperty("pageId", value);
        }

        /// <summary>
        /// The concept of "document" is somewhat vague, we can have multiple physical files
        /// or multiple sources that constitute a document, this is an opaque value that will
        /// be returned to the user.
        /// </summary>
        public string Tag
        {
            get => base.GetStringProperty("tag") ?? string.Empty;
            set => AddStringProperty("tag", value);
        }
    }
}
