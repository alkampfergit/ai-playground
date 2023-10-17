using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.DocumentChat;
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
        public async Task<ActionResult> SingleMessage(SegmentedDocumentDto doc)
        {
            //first of all we will delete everything
            var segmentSearch = new SegmentsSearch(_documentsConfig.CurrentValue.DocumentSegmentsIndexName);
            segmentSearch.DocId = new string[] { doc.DocumentId };
            await _elasticSearchService.DeleteSegmentsByQueryAsync(segmentSearch);

            //now we need to index all data in docs
            var segments = doc.Segments
                .Select(x => new ElasticDocumentSegment(doc.DocumentId, x.Content, x.PageId) { Tag = x.Tag })
                .ToList();
            var result = await _elasticSearchService.IndexAsync(_documentsConfig.CurrentValue.DocumentSegmentsIndexName, segments);
            if (!result)
            {
                return StatusCode(500, new { Error = "Internal error indexing data"});
            }

            return Ok();
        }

        [HttpPost]
        [Route("search")]
        public async Task<ActionResult> SearchSegments(SegmentsSearchDto dto)
        {
            var segmentsSearch = dto.ToSegmentsSearch(_documentsConfig.CurrentValue.DocumentSegmentsIndexName);
            var result = await _elasticSearchService.SearchSegmentsAsync(segmentsSearch);

            //now translate results into SegmentMatchDto
            var segmentMatches = result.Select(x => new SegmentMatchDto
            {
                DocId = x.DocumentId,
                Content = x.Content,
                Page = x.PageId,
                Tag = x.Tag,
            }).ToList();

            return Ok(segmentMatches);
        }
    }
}
