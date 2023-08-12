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
            List<SingleDenseVectorData> vectors = new List<SingleDenseVectorData>();

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

                    if (page.Gpt35PageInformation.Ner?.Count > 0)
                    {
                        elasticDocument.AddStringProperty("ner", page.Gpt35PageInformation.Ner);
                    }

                    elasticDocument.AddTextProperty("code", page.Gpt35PageInformation.Code);
                }

                //Ok we need to add all embeddings.
                if (allEmbeddings.TryGetValue(page.Number, out var pageEmbeddings))
                {
                    foreach (var embedding in pageEmbeddings)
                    {
                        SingleDenseVectorData singleDenseVectorData = new SingleDenseVectorData(
                            Id: pageId,
                            FieldName: embedding.Model,
                            VectorData: embedding.Vector.ToArray(),
                            NormalizedVectorData: embedding.VectorNormalized.ToArray(),
                            Gpt35VectorData: embedding.VectorGpt35.ToArray(),
                            Gpt35NormalizedVectorData: embedding.VectorGpt35Normalized.ToArray()
                        );
                        vectors.Add(singleDenseVectorData);
                    }
                }

                pages.Add(elasticDocument);
            }

            await _elasticSearchService.IndexAsync(IndexName, pages);
            Logger.Information("Indexed document {documentId} with {pages} pages", rawDocument.Id, pages.Count);

            //now index all dense vectors in blocks
            if (vectors.Count > 0)
            {
                var batchSize = 50;
                var vectorsPerId = vectors.GroupBy(v => v.Id)
                     .ToDictionary(g => g.Key, g => g.ToList());
                int total = 0;

                var startIndex = 0;
                while (startIndex < vectors.Count)
                {
                    var batch = vectors.Skip(startIndex).Take(batchSize).ToList();
                    await _elasticSearchService.IndexDenseVectorAsync(IndexName, batch.ToArray());
                    total += batch.Count;
                    Logger.Information("Indexed {batchSize} vectors on a total of {total} for document {documentId}", total, vectors.Count, rawDocument.Id);
                    startIndex += batchSize;
                }
            }
            Logger.Information("Indexed in elastic with {pages} pages - document id {document}", pages.Count, rawDocument.Id);
        }
    }
}
