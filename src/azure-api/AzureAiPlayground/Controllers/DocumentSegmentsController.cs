using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Documents;
using AzureAiLibrary.Helpers;
using AzureAiPlayground.Controllers.Models;
using AzureAiPlayground.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureAiPlayground.Controllers
{
    [ApiController]
    [Route("/api/documents-segments")]
    public class DocumentSegmentsController : Controller
    {
        private readonly IOptionsMonitor<AzureOpenAiConfiguration> _azureOpenAiConfiguration;
        private readonly TemplateHelper _templateHelper;
        private readonly ChatClient _chatClient;
        private readonly ElasticSearchService _elasticSearchService;
        private readonly IOptionsMonitor<DocumentsConfig> _documentsConfig;
        private readonly FolderDatabase<ApiPayload> _db;

        public DocumentSegmentsController(
            FolderDatabaseFactory folderDatabaseFactory,
            TemplateHelper templateHelper,
            ChatClient chatClient,
            ElasticSearchService elasticSearchService,
            IOptionsMonitor<DocumentsConfig> documentsConfig,
            IOptionsMonitor<AzureOpenAiConfiguration> azureOpenAiConfiguration)
        {
            _azureOpenAiConfiguration = azureOpenAiConfiguration;
            _templateHelper = templateHelper;
            _chatClient = chatClient;
            _elasticSearchService = elasticSearchService;
            _documentsConfig = documentsConfig;
            _db = folderDatabaseFactory.CreateDb<ApiPayload>();
        }

        [HttpPost]
        [Route("index-document")]
        public async Task<ActionResult> SingleMessage(ChatRequestMessage dto)
        {
            var message = _templateHelper.ExpandTemplates(dto.Message);
            var payload = new ApiPayload
            {
                Messages = new List<Message>()
                {
                    new Message("system", "You are an helpful AI"),
                    new Message("user", message),
                },
                MaxTokens = 800,
                Temperature = 0.5,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
                TopP = 0.95,
                Stop = null
            };
            var response = await _chatClient.SendMessageAsync(_azureOpenAiConfiguration.CurrentValue.Default, payload);

            return Ok(response.Content);
        }
    }
}
