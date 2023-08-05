using MongoDB.Driver;

namespace AzureAiLibrary.Documents.Jobs
{
    public class ElasticSearchIndexerJob : BaseJob
    {
        private readonly ElasticSearchService _elasticSearchService;
        private readonly IMongoCollection<MongoEmbedding> _embeddingCollection;

        protected override string PollerProperty => "IndexToElastic";

        /// <summary>
        /// Todo take this from config
        /// </summary>
        public const string IndexName = "aidocuments";

        public ElasticSearchIndexerJob(IMongoDatabase db, ElasticSearchService elasticSearchService) : base(db)
        {
            _elasticSearchService = elasticSearchService;
            _embeddingCollection = db.GetCollection<MongoEmbedding>("documents_embeddings");
        }

        protected override async Task OnBeforeStart()
        {
            await _elasticSearchService.InitIndexAsync(IndexName);
        }

        protected override async Task InnerPerformTask(MongoDocumentToIndex rawDocument)
        {
            //This simply insert document into elastic, we should batch insertion but for
            //this POC we can index one by one.
            List<ElasticDocument> pages = new List<ElasticDocument>();
            Logger.Information("About to index in elastic {document}", rawDocument.Id);

            var allEmbeddings = _embeddingCollection
                .AsQueryable()
                .Where(e => e.DocumentId == rawDocument.Id)
                .ToList()
                .GroupBy(m => m.PageNumber)
                .ToDictionary(p => p.Key, p => p.ToList());

            foreach (var page in rawDocument.Pages)
            {
                var pageId = $"{page.Number}_{rawDocument.Id}";
                var elasticDocument = new ElasticDocument(pageId);
                elasticDocument.AddTextProperty("originalcontent", page.OriginalContent);
                elasticDocument.AddTextProperty("content", page.Content);

                if (!String.IsNullOrEmpty(page.Gpt35PageInformation?.CleanText))
                {
                    elasticDocument.AddTextProperty("gpt35content", page.Gpt35PageInformation.CleanText);
                    elasticDocument.AddTextProperty("code", page.Gpt35PageInformation.Code);
                    //Add support for array property and add NER.
                }

                if (allEmbeddings.TryGetValue(page.Number, out var embedding)) 
                {
                //TODO INDEX
                }

                pages.Add(elasticDocument);
            }

            await _elasticSearchService.IndexAsync(IndexName, pages);
            Logger.Information("Indexed in elastic with {pages} pages - document id {document}", pages.Count, rawDocument.Id);
        }
    }
}
