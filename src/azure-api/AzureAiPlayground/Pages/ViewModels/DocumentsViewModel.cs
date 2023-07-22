using AzureAiLibrary.Configuration;
using AzureAiLibrary.Documents;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AzureAiPlayground.Pages.ViewModels
{
    public class DocumentsViewModel
    {
        private IMongoDatabase _mongoDatabase;
        private TikaExtractor _tikaExtractor;
        private RawDocumentSimpleReader _rawDocumentSimpleReader;

        public DocumentsViewModel(IOptionsMonitor<DocumentsConfig> documentsConfig)
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

            _rawDocumentSimpleReader = new RawDocumentSimpleReader(_mongoDatabase);
        }

        public Task ExtractPath(string path, string filter)
        {
            return _tikaExtractor.Extract(path, filter);
        }

        internal Task StartServices()
        {
            _rawDocumentSimpleReader.Start();
            return Task.CompletedTask;
        }
    }
}
