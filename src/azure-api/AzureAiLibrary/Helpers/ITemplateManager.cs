
using AzureAiLibrary.Configuration;
using Microsoft.Extensions.Options;

namespace AzureAiLibrary.Helpers
{
    public  interface ITemplateManager
    {
        string GetTemplateContent(string templateName);
    }

    public class DefaultTemplateManager : ITemplateManager
    {
        private readonly IOptionsMonitor<ChatConfig> _config;

        public DefaultTemplateManager(IOptionsMonitor<ChatConfig> config)
        {
            _config = config;
        }

        public string GetTemplateContent(string templateName)
        {
            return _config.CurrentValue.GetTemplateContent(templateName) ?? string.Empty;
        }
    }
}
