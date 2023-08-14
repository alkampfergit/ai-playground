using AzureAiLibrary.Documents;
using MongoDB.Driver;

namespace AzureAiLibrary.Tests
{
    public abstract class DatabaseTestClass : IDisposable
    {
        protected MongoClient _client;
        protected IMongoDatabase _db;
        protected IMongoCollection<MongoDocumentToIndex> _documentToIndexCollection;

        static DatabaseTestClass()
        {
            //I need to delete all database tht start with Azure_ai_library_test_
            var connection = Environment.GetEnvironmentVariable("TEST_MONGODB");
            var mongoUrlBuilder = new MongoDB.Driver.MongoUrlBuilder(connection);
            var client = new MongoClient(mongoUrlBuilder.ToMongoUrl());
            var db = client.GetDatabase("admin");
            var databases = client.ListDatabasesAsync().Result.ToListAsync().Result;
            foreach (var database in databases)
            {
                if (database["name"].ToString().StartsWith("Azure_ai_library_test_"))
                {
                    client.DropDatabase(database["name"].ToString());
                }
            }
        }

        public DatabaseTestClass()
        {
            var connection = Environment.GetEnvironmentVariable("TEST_MONGODB");
            var mongoUrlBuilder = new MongoDB.Driver.MongoUrlBuilder(connection);
            if (!string.IsNullOrEmpty(mongoUrlBuilder.Username))
            {
                //well we have a login 
                mongoUrlBuilder.AuthenticationSource = "admin";
            }
            mongoUrlBuilder.DatabaseName = "Azure_ai_library_test_" + Guid.NewGuid().ToString();
            _client = new MongoClient(mongoUrlBuilder.ToMongoUrl());
            _db = _client.GetDatabase(mongoUrlBuilder.DatabaseName);
            _documentToIndexCollection = _db.GetCollection<MongoDocumentToIndex>("documents_to_index");
        }

        public void Dispose()
        {
            _client.DropDatabase(_db.DatabaseNamespace.DatabaseName);
            OnDispose();
        }

        protected virtual void OnDispose()
        {
        }
    }
}
