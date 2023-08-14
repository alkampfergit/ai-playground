using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.Jobs;
using MongoDB.Driver;

namespace AzureAiLibrary.Helpers
{
    public static class MongoDbHelper
    {
        public static Task UpdateSinglePage(this IMongoCollection<MongoDocumentToIndex> collection, string id, DocumentPage page)
        {
            var filter = Builders<MongoDocumentToIndex>.Filter.And(
                Builders<MongoDocumentToIndex>.Filter.Eq("_id", id),
                Builders<MongoDocumentToIndex>.Filter.Eq("Pages.Number", page.Number)
);
            var update = Builders<MongoDocumentToIndex>.Update.Set("Pages.$", page);
            return collection.UpdateOneAsync(filter, update);
        }

        public static Task UpdateSinglePageGpt35Information(this IMongoCollection<MongoDocumentToIndex> collection, string id, int pageNumber, Gpt35PageInformation gpt35PageInformation)
        {
            var filter = Builders<MongoDocumentToIndex>.Filter.And(
                Builders<MongoDocumentToIndex>.Filter.Eq("_id", id),
                Builders<MongoDocumentToIndex>.Filter.Eq("Pages.Number", pageNumber)
            );
            var update = Builders<MongoDocumentToIndex>.Update.Set("Pages.$.Gpt35PageInformation", gpt35PageInformation);
            return collection.UpdateOneAsync(filter, update);
        }
    }
}
