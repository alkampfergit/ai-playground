using AzureAiLibrary.Documents;
using MongoDB.Driver;

namespace AzureAiLibrary.Tests.Documents
{
    public class DocumentToIndexManagerTests : DatabaseTestClass
    {
        private DocumentToIndexManager _sut;

        public DocumentToIndexManagerTests()
        {
            _sut = new DocumentToIndexManager(_db);
        }

        [Fact]
        public async Task Can_schedule_single_gpt35_cleanup()
        {
            MongoDocumentToIndex mongoDocumentToIndex = await CreateADocument();

            MongoDocumentToIndex mongoDocumentToIndex2 = await CreateADocument();

            var started = DateTime.UtcNow.AddSeconds(-1);

            //Act, schedule cleanup gpt for the first document
            await _sut.QueueForGpt35Cleanup(mongoDocumentToIndex.Id);

            //Assert
            var scheduled = _documentToIndexCollection.Find(x => x.Id == mongoDocumentToIndex.Id).FirstOrDefault();
            Assert.False(scheduled.Processing);
            //Assert that scheduled.CleanWithGpt35 is greater than when the test started
            Assert.True(scheduled.CleanWithGpt35 >= started, $"scheduled date must be greater than {started} but is {scheduled.CleanWithGpt35}");

            //Verify second document is null.
            var scheduled2 = _documentToIndexCollection.Find(x => x.Id == mongoDocumentToIndex2.Id).FirstOrDefault();
            Assert.False(scheduled2.Processing);
            //Assert that scheduled.CleanWithGpt35 is greater than when the test started
            Assert.Null(scheduled2.CleanWithGpt35);
        }

        [Fact]
        public async Task Can_schedule_global_gpt35_cleanup()
        {
            MongoDocumentToIndex mongoDocumentToIndex = await CreateADocument();

            MongoDocumentToIndex mongoDocumentToIndex2 = await CreateADocument();

            var started = DateTime.UtcNow.AddSeconds(-1);

            //Act, schedule cleanup gpt for the first document
            await _sut.QueueForGpt35Cleanup(id: null);

            //Assert
            var scheduled = _documentToIndexCollection.Find(x => x.Id == mongoDocumentToIndex.Id).FirstOrDefault();
            Assert.False(scheduled.Processing);
            //Assert that scheduled.CleanWithGpt35 is greater than when the test started
            Assert.True(scheduled.CleanWithGpt35 >= started, $"scheduled date must be greater than {started} [{started.Ticks}] but is {scheduled.CleanWithGpt35}[{scheduled.CleanWithGpt35.Value.Ticks}]");

            //Verify second document is null.
            var scheduled2 = _documentToIndexCollection.Find(x => x.Id == mongoDocumentToIndex2.Id).FirstOrDefault();
            Assert.False(scheduled2.Processing);
            //Assert that scheduled.CleanWithGpt35 is greater than when the test started
            Assert.True(scheduled2.CleanWithGpt35 >= started, $"scheduled date must be greater than {started} [{started.Ticks}]  but is {scheduled.CleanWithGpt35}[{scheduled.CleanWithGpt35.Value.Ticks}]");
        }

        [Fact]
        public async Task Can_schedule_embedding()
        {
            MongoDocumentToIndex mongoDocumentToIndex = await CreateADocument();

            MongoDocumentToIndex mongoDocumentToIndex2 = await CreateADocument();

            var started = DateTime.UtcNow.AddSeconds(-1);

            //Act, schedule cleanup gpt for the first document
            await _sut.QueueForEmbedding(mongoDocumentToIndex.Id, "sentence-transformers/distiluse-base-multilingual-cased-v1", "berttest");

            //Assert
            var scheduled = _documentToIndexCollection.Find(x => x.Id == mongoDocumentToIndex.Id).FirstOrDefault();
            Assert.False(scheduled.Processing);

            Assert.True(scheduled.Embedding >= started, $"scheduled date must be greater than {started} [{started.Ticks}] but is {scheduled.Embedding}[{scheduled.Embedding.Value.Ticks}]");
            Assert.Equal("sentence-transformers/distiluse-base-multilingual-cased-v1", scheduled.EmbeddingModel);

            //Verify second document is null.
            var scheduled2 = _documentToIndexCollection.Find(x => x.Id == mongoDocumentToIndex2.Id).FirstOrDefault();
            Assert.False(scheduled2.Processing);

            Assert.Null(scheduled2.Embedding);
            Assert.Null(scheduled2.EmbeddingModel);
        }

        [Fact]
        public async Task Can_schedule_embedding_for_all()
        {
            MongoDocumentToIndex mongoDocumentToIndex = await CreateADocument();

            MongoDocumentToIndex mongoDocumentToIndex2 = await CreateADocument();

            var started = DateTime.UtcNow.AddSeconds(-1);

            //Act, schedule cleanup gpt for the first document
            await _sut.QueueForEmbedding(id: null, embeddingModel : "sentence-transformers/distiluse-base-multilingual-cased-v1", embeddingModelKey: "berttest");

            //Assert
            var scheduled = _documentToIndexCollection.Find(x => x.Id == mongoDocumentToIndex.Id).FirstOrDefault();
            Assert.False(scheduled.Processing);

            Assert.True(scheduled.Embedding >= started, $"scheduled date must be greater than {started} but is {scheduled.Embedding}");
            Assert.Equal("sentence-transformers/distiluse-base-multilingual-cased-v1", scheduled.EmbeddingModel);

            //Verify second document is null.
            var scheduled2 = _documentToIndexCollection.Find(x => x.Id == mongoDocumentToIndex2.Id).FirstOrDefault();
            Assert.False(scheduled2.Processing);

            Assert.True(scheduled2.Embedding >= started, $"scheduled date must be greater than {started} but is {scheduled.Embedding}");
            Assert.Equal("sentence-transformers/distiluse-base-multilingual-cased-v1", scheduled2.EmbeddingModel);
        }

        private async Task<MongoDocumentToIndex> CreateADocument()
        {
            MongoDocumentToIndex mongoDocumentToIndex = new MongoDocumentToIndex()
            {
                Id = Guid.NewGuid().ToString(),
                Pages = new List<DocumentPage>()
                {
                    new DocumentPage(1, false, "this is a content"),
                    new DocumentPage(2, false, "this is a content in page 2"),
                }
            };

            await _documentToIndexCollection.InsertOneAsync(mongoDocumentToIndex);
            return mongoDocumentToIndex;
        }
    }
}
