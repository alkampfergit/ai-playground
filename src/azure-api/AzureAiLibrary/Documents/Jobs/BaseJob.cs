using AzureAiLibrary.Documents.Support;
using MongoDB.Driver;
using Serilog;

namespace AzureAiLibrary.Documents.Jobs
{
    public abstract class BaseJob
    {
        private readonly IMongoCollection<MongoDocumentToIndex> _documentsToIndex;
        private readonly PollerHelper _poller;

        protected readonly ILogger Logger;

        protected BaseJob(IMongoDatabase db)
        {
            Logger = Log.ForContext(GetType());
            _documentsToIndex = db.GetCollection<MongoDocumentToIndex>("documents_to_index");

            // ReSharper disable VirtualMemberCallInConstructor
            if (PollerProperty == null)
            {
                throw new System.Exception("PollerProperty must be set in the derived class");
            }

            _documentsToIndex.Indexes.CreateOne(
                new CreateIndexModel<MongoDocumentToIndex>(
                    Builders<MongoDocumentToIndex>.IndexKeys.Ascending(PollerProperty),
                    new CreateIndexOptions
                    {
                        Sparse = true,
                        Background = true,
                        Name = $"Polling_{PollerProperty}"
                    }));

            _poller = new PollerHelper(PollAsync, 2000, GetType().Name);
        }

        private async Task PollAsync()
        {
            //Filter documents for this job.
            var filter = Builders<MongoDocumentToIndex>.Filter.And(
                Builders<MongoDocumentToIndex>.Filter.Lt(PollerProperty, DateTime.UtcNow),
                Builders<MongoDocumentToIndex>.Filter.Ne(d => d.Processing, true));

            var update = Builders<MongoDocumentToIndex>.Update
                .Set(PollerProperty, DateTime.UtcNow.AddMinutes(10))
                .Set(d => d.Processing, true);
            var options = new FindOneAndUpdateOptions<MongoDocumentToIndex>
            {
                ReturnDocument = ReturnDocument.After
            };

            //Execute the query and process file until no documents is returned anymore
            MongoDocumentToIndex rawDocument;
            while ((rawDocument = await _documentsToIndex.FindOneAndUpdateAsync(filter, update, options)) != null)
            {
                try
                {
                    await InnerPerformTask(rawDocument);
                    //Update the whole document
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error while processing document {document}", rawDocument.Id);
                    //we could not update the document, log the error and continue.
                    //TODO: Put the error somewhere in the database to diagnostics
                }
                finally
                {
                    rawDocument.IndexToElastic =  DateTime.UtcNow; //need to be reindexed in elastic.
                    var updateResult = await _documentsToIndex.ReplaceOneAsync(
                         Builders<MongoDocumentToIndex>.Filter.Eq(x => x.Id, rawDocument.Id),
                         rawDocument);

                    //unlock the document another query because we need simply to update two elements.
                    await _documentsToIndex.UpdateOneAsync(
                        Builders<MongoDocumentToIndex>.Filter.Eq(x => x.Id, rawDocument.Id),
                        Builders<MongoDocumentToIndex>.Update
                            .Unset(PollerProperty)
                            .Set(x => x.Processing, false));
                }
            }
        }

        /// <summary>
        /// Really perform the task modifying the document.
        /// </summary>
        /// <param name="rawDocument"></param>
        /// <returns></returns>
        protected abstract Task InnerPerformTask(MongoDocumentToIndex rawDocument);

        protected abstract string PollerProperty { get; }

        public async Task Start()
        {
            await OnBeforeStart();
            _poller.Start();
        }

        protected virtual Task OnBeforeStart()
        {
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            return _poller.StopAsync(true);
        }
    }
}
