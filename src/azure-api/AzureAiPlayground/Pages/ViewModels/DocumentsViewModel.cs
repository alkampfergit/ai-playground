using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.Jobs;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AzureAiPlayground.Pages.ViewModels
{
    public class DocumentsViewModel
    {
        private readonly Gpt35AiCleaner _gpt35AiCleaner;
        private readonly ElasticSearchIndexerJob _elasticIndexer;
        private IMongoDatabase _mongoDatabase;
        private TikaExtractor _tikaExtractor;
        private RawDocumentSimpleReader _rawDocumentSimpleReader;

        public DocumentsViewModel(
            IOptionsMonitor<DocumentsConfig> documentsConfig,
            ChatClient chatClient)
        {
            var url = new MongoUrl(documentsConfig.CurrentValue.MongoUrl);
            var settings = MongoClientSettings.FromUrl(url);
            var client = new MongoClient(settings);
            _mongoDatabase = client.GetDatabase(url.DatabaseName);
            _tikaExtractor = new TikaExtractor(
                new TikaOutOfProcess(
                   documentsConfig.CurrentValue.JavaBin,
                   documentsConfig.CurrentValue.Tika),
                _mongoDatabase);

            //TODO: Register database and register this in the ui container.
            _rawDocumentSimpleReader = new RawDocumentSimpleReader(_mongoDatabase);

            //TODO register all jobs and resolve all
            _gpt35AiCleaner = new Gpt35AiCleaner(_mongoDatabase, chatClient);
            _elasticIndexer = new ElasticSearchIndexerJob(_mongoDatabase, new ElasticSearchService(new Uri(documentsConfig.CurrentValue.ElasticUrl)));
        }

        public Task ExtractPath(string path, string filter)
        {
            return _tikaExtractor.Extract(path, filter);
        }

        internal async Task StartServices()
        {
            _rawDocumentSimpleReader.Start();
            await _gpt35AiCleaner.Start();
            await _elasticIndexer.Start();
        }
    }
}
