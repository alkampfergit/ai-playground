using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.Jobs;
using AzureAiLibrary.Helpers;
using MongoDB.Driver;

namespace AzureAiLibrary.Tests.Helpers
{
    public class MongoDbHelperTests : IDisposable
    {
        private MongoClient _client;
        private IMongoDatabase _db;
        private IMongoCollection<MongoDocumentToIndex> _collection;

        public MongoDbHelperTests()
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
            _collection = _db.GetCollection<MongoDocumentToIndex>("documents");
        }

        public void Dispose()
        {
            _client.DropDatabase(_db.DatabaseNamespace.DatabaseName);
        }

        [Fact]
        public void CanSave_single_page()
        {
            MongoDocumentToIndex document = new MongoDocumentToIndex()
            {
                Id = Guid.NewGuid().ToString(),
                Pages = new List<DocumentPage>()
                {
                    new DocumentPage(1, false, "this is a content"),
                    new DocumentPage(2, false, "this is the original content"),
                }
            };
            _collection.InsertOne(document);

            //Act update only page 2
            document.Pages[1].Content = "this is a new content in page 2";
            _collection.UpdateSinglePage(document.Id, document.Pages[1]);

            //Assert verify that everything is allright
            var result = _collection.Find(x => x.Id == document.Id).FirstOrDefault();
            Assert.Equal("this is a new content in page 2", result.Pages[1].Content);
            Assert.Equal("this is the original content", result.Pages[1].OriginalContent);

            //old page content remains unchanged
            Assert.Equal("this is a content", result.Pages[0].OriginalContent);
        }

        [Fact]
        public void CanSave_only_gpt35_page_information()
        {
            MongoDocumentToIndex document = new MongoDocumentToIndex()
            {
                Id = Guid.NewGuid().ToString(),
                Pages = new List<DocumentPage>()
                {
                    new DocumentPage(1, false, "this is a content"),
                    new DocumentPage(2, false, "this is a content in page 2"),
                }
            };
            _collection.InsertOne(document);

            //Act update only page 2
            var gpt35Info = new Gpt35PageInformation()
            {
                CleanText = "this is a new content in page 2",
                Code = "code",
                Ner = new List<string> { "bla", "Bleh" }
            };
            _collection.UpdateSinglePageGpt35Information(document.Id, document.Pages[1].Number, gpt35Info);

            //Assert verify that everything is allright
            var result = _collection.Find(x => x.Id == document.Id).FirstOrDefault();
            var gpt35InfoResult = result.Pages[1].Gpt35PageInformation;
            Assert.Equal(gpt35Info.CleanText, gpt35InfoResult.CleanText);
            Assert.Equal(gpt35Info.Code, gpt35InfoResult.Code);
            Assert.Equal(gpt35Info.Ner, gpt35InfoResult.Ner);
        
            //Assert other properties remains unchanged
            Assert.Equal("this is a content", result.Pages[0].OriginalContent);
            Assert.Equal("this is a content in page 2", result.Pages[1].OriginalContent);
        }
    }
}
