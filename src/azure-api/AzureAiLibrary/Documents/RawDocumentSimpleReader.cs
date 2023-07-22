using AzureAiLibrary.Documents.Support;
using HtmlAgilityPack;
using MongoDB.Driver;
using Serilog;

namespace AzureAiLibrary.Documents
{
    public class RawDocumentSimpleReader
    {
        private ILogger Logger = Log.ForContext<TikaExtractor>();
        private IMongoCollection<MongoRawDocument> _rawDocuments;
        private IMongoCollection<MongoDocumentToIndex> _documentsToIndex;
        private PollerHelper _poller;

        public RawDocumentSimpleReader(
            IMongoDatabase db)
        {
            _rawDocuments = db.GetCollection<MongoRawDocument>("raw_documents");
            _documentsToIndex = db.GetCollection<MongoDocumentToIndex>("documents_to_index");

            //Fire and forget
            _documentsToIndex.Indexes.CreateOne(
                new CreateIndexModel<MongoDocumentToIndex>(
                    Builders<MongoDocumentToIndex>.IndexKeys.Ascending(x => x.Elastic),
                    new CreateIndexOptions
                    {
                        Sparse = true,
                        Background = true,
                        Name = "Elastic"
                    }));

            _poller = new PollerHelper(PollAsync, 2000, GetType().Name);
        }

        private async Task PollAsync()
        {
            //ok I need to poll the original raw documents for documents to be processed.
            //I need to find document that hase Analyze property less than current timestamp
            //and with findOneAndUpdate I need to update the Analyze property 10 minutes in the future
            //so if the polling crashes the document will be picked up again
            var filter = Builders<MongoRawDocument>.Filter.Lt(x => x.Analyze, DateTime.UtcNow);
            var update = Builders<MongoRawDocument>.Update.Set(x => x.Analyze, DateTime.UtcNow.AddMinutes(10));
            var options = new FindOneAndUpdateOptions<MongoRawDocument>
            {
                ReturnDocument = ReturnDocument.After
            };

            //Execute the query and process file until no documents is returned anymore
            MongoRawDocument rawDocument;
            while ((rawDocument = await _rawDocuments.FindOneAndUpdateAsync(filter, update, options)) != null)
            {
                try
                {
                    //ok this is really simple we simply gete the data extracted from raw documetn and simply get the inner text
                    //from html and save it in the document to index collection
                    var pages = rawDocument.Pages
                        .Select(x =>
                        {
                            var doc = new HtmlDocument();
                            doc.LoadHtml(x);
                            return doc.DocumentNode.InnerText;
                        }).ToList();

                    var documentToIndex = new MongoDocumentToIndex()
                    {
                        Id = rawDocument.Id,
                        Pages = pages,
                        Metadata = rawDocument.Metadata
                    };
                    //now simply insert into the collection signaling that the document is ready to be inserted into elastic
                    documentToIndex.Elastic = DateTime.UtcNow;

                    //upsert the document
                    await _documentsToIndex.ReplaceOneAsync(
                        x => x.Id == documentToIndex.Id,
                        documentToIndex,
                        new ReplaceOptions { IsUpsert = true });

                    //now remove the analyze property to avoid being picked up again
                    await _rawDocuments.UpdateOneAsync(
                        x => x.Id == rawDocument.Id,
                        Builders<MongoRawDocument>.Update.Unset(x => x.Analyze));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error while processing document {document}", rawDocument.Id);
                }
            }
        }

        public void Start()
        {
            _poller.Start();
        }

        public Task StopAsync()
        {
            return _poller.StopAsync(true);
        }
    }
}
