using AzureAiLibrary.Configuration;
using AzureAiLibrary.Helpers;
using Microsoft.Extensions.Options;

namespace AzureAiPlayground.Support
{
    public class FolderDatabaseFactory
    {
        private readonly IOptions<ChatConfig> _config;

        public FolderDatabaseFactory(IOptions<ChatConfig> config)
        {
            _config = config;
        }

        public FolderDatabase<T> CreateDb<T>() where T : class
        {
            string databaseFolder = Path.Combine(_config.Value.DataDir, typeof(T).Name);
            if (!Directory.Exists(databaseFolder)) Directory.CreateDirectory(databaseFolder);
            return new FolderDatabase<T>(databaseFolder);
        }
    }
}
