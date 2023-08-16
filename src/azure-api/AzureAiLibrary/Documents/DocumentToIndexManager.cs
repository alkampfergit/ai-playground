using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace AzureAiLibrary.Documents
{
    public class DocumentToIndexManager
    {
        private IMongoCollection<MongoDocumentToIndex> _documentsToIndex;

        public DocumentToIndexManager(IMongoDatabase db)
        {
            _documentsToIndex = db.GetCollection<MongoDocumentToIndex>("documents_to_index");
        }

        /// <summary>
        /// Queue for embedding just specify a null <paramref name="embeddingModel"/> to queue ALL documents.
        /// Please queue all documents only if you really needs to.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="embeddingModel"></param>
        /// <returns></returns>
        public async Task QueueForEmbedding(string? id, string embeddingModel, string embeddingModelKey)
        {
            //need to set processing to false and CleanWithGpt35 to datetime.utcnow for specified document
            FilterDefinition<MongoDocumentToIndex> filter;
            if (!String.IsNullOrEmpty(id))
            {
                filter = Builders<MongoDocumentToIndex>.Filter.Eq(x => x.Id, id);
            }
            else
            {
                filter = Builders<MongoDocumentToIndex>.Filter.Empty;
            }
            var update = Builders<MongoDocumentToIndex>.Update
                .Set(x => x.Processing, false)
                .Set(x => x.Embedding, DateTime.UtcNow)
                .Set(x => x.EmbeddingModel, embeddingModel)
                .Set(x => x.EmbeddingModelKey, embeddingModelKey);

            var documentSerializer = BsonSerializer.SerializerRegistry.GetSerializer<MongoDocumentToIndex>();
            var query = filter.Render(documentSerializer, BsonSerializer.SerializerRegistry);

            var result = await _documentsToIndex.UpdateManyAsync(filter, update);
        }

        /// <summary>
        /// Queue a single document for processing GPT35 or queue all documents that needs to be
        /// processing.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task QueueForGpt35Cleanup(string? id)
        {
            //need to set processing to false and CleanWithGpt35 to datetime.utcnow for specified document
            FilterDefinition<MongoDocumentToIndex> filter;
            if (!String.IsNullOrEmpty(id))
            {
                filter = Builders<MongoDocumentToIndex>.Filter.Eq(x => x.Id, id);
            }
            else
            {
                filter = Builders<MongoDocumentToIndex>.Filter.Eq("Pages.Gpt35PageInformation", (object) null);
            }
            var update = Builders<MongoDocumentToIndex>.Update
                .Set(x => x.Processing, false)
                .Set(x => x.CleanWithGpt35, DateTime.UtcNow);

            var documentSerializer = BsonSerializer.SerializerRegistry.GetSerializer<MongoDocumentToIndex>();
            var query = filter.Render(documentSerializer, BsonSerializer.SerializerRegistry);

            var result = await _documentsToIndex.UpdateManyAsync(filter, update);
        }
    }
}
