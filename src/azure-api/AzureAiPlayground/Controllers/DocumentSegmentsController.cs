using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.DocumentChat;
using AzureAiLibrary.Helpers;
using AzureAiPlayground.Controllers.Models;
using AzureAiPlayground.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

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

        private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<DocumentSegmentsController>();

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
        public async Task<ActionResult> IndexDocument(SegmentedDocumentDto doc)
        {
            //first of all we will delete everything
            var segmentSearch = new SegmentsSearch(_documentsConfig.CurrentValue.DocumentSegmentsIndexName);
            segmentSearch.DocId = new string[] { doc.DocumentId };
            await _elasticSearchService.DeleteSegmentsByQueryAsync(segmentSearch);

            //now we need to segment with a custom segmenter, we group by tag.
            var groups = doc.Segments.GroupBy(x => x.Tag);
            var segmenter = new Segmenter(400, 20);

            List<ElasticDocumentSegment> elasticSsgments = new();
            foreach (var group in groups)
            {
                //Create a series of segments with the correct numbers of token.
                var segments = segmenter.Segment(group.OrderBy(s => s.PageId).Select(x => x.Content));
                foreach (var segment in segments)
                {
                    elasticSsgments.Add(new ElasticDocumentSegment(doc.DocumentId, segment.Content, segment.Index)
                    {
                        Tag = group.Key
                    });
                }
            }

            var result = await _elasticSearchService.IndexAsync(_documentsConfig.CurrentValue.DocumentSegmentsIndexName, elasticSsgments);
            if (!result)
            {
                return StatusCode(500, new { Error = "Internal error indexing data" });
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

        [HttpPost]
        [Route("doQuestion")]
        public async Task<ActionResult> Question(DocumentSegmentsQuestionsDto dto)
        {
            var segmentSearch = dto.ToSegmentSearch(_documentsConfig.CurrentValue.DocumentSegmentsIndexName);
            List<DebugStep> logs = new List<DebugStep>();
            //perform the query raw
            var result = await _elasticSearchService.SearchSegmentsAsync(segmentSearch);

            if (result.Count == 0)
            {
                logs.Add(new DebugStep("search-result", "The query returned no segments"));
                return Ok(new DocumentSegmentsAnswerDto() { Answer  = "No piece of relevant text found", DebugSteps = Array.Empty<DebugStep>() });
            }

            //create the context grouping first 4 results
            StringBuilder sb = new StringBuilder();
            //we need to include context up to 3000 tokens
            int totalTokens = 0;
            foreach (var item in result)
            {
                var tokens = TikTokenTokenizer.GetTokenCount(item.Content);
                if (totalTokens + tokens > 3000) break;
                totalTokens += tokens;
                sb.AppendLine($"citation: {{\"DocId\": \"{item.DocumentId}\", \"Page\": \"{item.PageId}\", \"Tag\": \"{item.Tag}\"}}");
                sb.AppendLine(item.Content);
                sb.AppendLine("---");
            }

            var chatQuestion = @$"I'll give you a question that you will answer based on citations. Each citation begins with a citation section, your answer will be formatted in JSON with the following fields
Answer: the answer to the question
Citations: an array that contains citations used to answer the question
Citations:
""""""
{sb}
""""""
Question: {dto.Question}";

            var payload = new ApiPayload
            {
                Messages = new List<Message>
            {
                new Message { Role = "system", Content = "You are a chatbot that will answer questions based on a context included in the prompt. You will never user your memory to answer the question." },
                new Message { Role = "user", Content = chatQuestion }
            },
                MaxTokens = 500,
                Temperature = 0.8,
                FrequencyPenalty = 1,
                PresencePenalty = 2,
                TopP = 0.9,
                Stop = null
            };
            logs.Add(new DebugStep("GPT3.5 call - question/answer", payload.Dump()));
            var chatResult = await _chatClient.SendMessageAsync("gpt35", payload);
            logs.Add(new DebugStep("GPT3.5 result - question/answer", chatResult.Dump()));

            //try to parse answer as json, we need to extract citations
            var answer = chatResult.Content;
            IReadOnlyCollection<Citation>? citations = null;
            try
            {
                //try to parse.
                var answerJson = JsonSerializer.Deserialize<AnswerData>(answer);
                if (answerJson != null)
                {
                    answer = answerJson.Answer;
                    citations = answerJson.Citations;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Chat GPT did not anwer a valid json");
            }

            return Ok(new DocumentSegmentsAnswerDto()
            {
                Answer = answer,
                Citations = citations,
                DebugSteps = logs
            });
        }
    }
}
