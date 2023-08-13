using MongoDB.Driver;
using Polly;
using Polly.Retry;
using Serilog;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace AzureAiLibrary.Documents.Jobs
{
    /// <summary>
    /// A text cleaner that uses AI to clean the text of the pages extracted, this will actually
    /// improve the quality of the text using AI
    /// </summary>
    public class Gpt35AiCleaner : BaseJob
    {
        private readonly ChatClient _chatClient;

        private readonly ILogger _logger = Log.ForContext<Gpt35AiCleaner>();

        private readonly AsyncRetryPolicy _retryPolicy;

        protected override string PollerProperty => "CleanWithGpt35";

        public Gpt35AiCleaner(IMongoDatabase db, ChatClient chatClient) : base(db)
        {
            _chatClient = chatClient;
            _retryPolicy = Policy
                .Handle<Exception>(ex => !ex.Message.Contains("The response was filtered"))
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(5 * retryAttempt), (ex, time) =>
                {
                    _logger.Warning(ex, $"An error occured while cleaning text at the specified page. Retrying in {time}");
                });
        }

        private void CreateTplFlow()
        {
            _createBlock = new TransformBlock<MongoDocumentToIndex, IReadOnlyCollection<TplMessage>>(CreateBlockToIndex,
                new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1 });
            _spreadBlock = CreateUnbatcherBlock<TplMessage>();
            _createBlock.LinkTo(_spreadBlock, new DataflowLinkOptions() { PropagateCompletion = true });

            _translateBlock = new ActionBlock<TplMessage>(Translate, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 4,
            });
            _spreadBlock.LinkTo(_translateBlock, new DataflowLinkOptions() { PropagateCompletion = true });
        }

        private record TplMessage(DocumentPage Page, String Text, MongoDocumentToIndex document);

        private TransformBlock<MongoDocumentToIndex, IReadOnlyCollection<TplMessage>>? _createBlock;
        private IPropagatorBlock<IReadOnlyCollection<TplMessage>, TplMessage>? _spreadBlock;
        private ActionBlock<TplMessage>? _translateBlock;

        private IReadOnlyCollection<TplMessage> CreateBlockToIndex(MongoDocumentToIndex rawDocument)
        {
            List<TplMessage> retValue = new List<TplMessage>();
            var totalPages = rawDocument.Pages.Count(p => !p.Removed);
            foreach (var page in rawDocument.Pages.Where(p => !p.Removed))
            {
                if (page.Gpt35PageInformation != null) continue;
                StringBuilder text = new StringBuilder();
                if (page.Number > 0)
                {
                    var prevPage = rawDocument.Pages[page.Number - 1];
                    if (!prevPage.Removed && prevPage.Content?.Length > 50)
                    {
                        text.Append(prevPage.Content.Substring(Math.Max(prevPage.Content.Length - 50, 0)));
                    }
                }
                text.Append(page.Content);
                if (page.Number < rawDocument.Pages.Count - 1)
                {
                    var nextPage = rawDocument.Pages[page.Number + 1];
                    if (!nextPage.Removed && nextPage.Content?.Length > 50)
                    {
                        text.Append(nextPage.Content.Substring(0, Math.Min(50, nextPage.Content.Length)));
                    }
                }

                //now I have the text I need to clean with gpt
                Logger.Debug("Page {pageNum} will be cleaned with GPT35", page.Number, totalPages);
                retValue.Add(new TplMessage(page, text.ToString(), rawDocument));
            }
            return retValue;
        }

        public static IPropagatorBlock<IReadOnlyCollection<T>, T> CreateUnbatcherBlock<T>()
        {
            var block = new TransformManyBlock<IReadOnlyCollection<T>, T>(array => array);
            return block;
        }

        private async Task Translate(TplMessage message)
        {
            try
            {
                if (message.Page.Gpt35PageInformation?.IsValid() != true)
                {
                    _logger.Information("AiCleaner processing page {page} on a total of {total} for document {document}", message.Page.Number, message.document.Pages.Count, message.document.Id);
                    message.Page.Gpt35PageInformation = await _retryPolicy.ExecuteAsync(() => CleanPage(message.Text));
                }
                else
                {
                    _logger.Information("AiCleaner skipped because already processed page {page} for document {document}", message.Page.Number, message.document.Id);
                }
            }
            catch (Exception ex)
            {
                //Exception passed polly
                message.Page.Gpt35PageInformation = Gpt35PageInformation.ForError(ex.Message);
            }
        }

        protected override async Task InnerPerformTask(MongoDocumentToIndex rawDocument)
        {
            Logger.Information("About to clean {docId} with GPT3.5", rawDocument.Id);
            CreateTplFlow();
            rawDocument.CleanWithGpt35Errors = "";
            _createBlock.Post(rawDocument);

            //now I simply need to wait for all the block to finish.
            _createBlock.Complete();

            await _translateBlock.Completion;

            Logger.Information("Finished cleanning {docId} with GPT3.5", rawDocument.Id);
        }

        public async Task<Gpt35PageInformation?> CleanPage(string pageContent)
        {
            List<Message> messages = new List<Message>()
            {
                new Message(
                    "system",
                    "you are an assistant that will help cleaning text extracted from book"),
                new Message(
                    "user",
                    $@"You are an assistant that extracts informations in a json format from a text. Json will have these properties.   
CleanText: summarize the text. Final text must be no less than fifty percent of the original code.  
Code: Brief explanation of computer code if code is present, null otherwise  
Ner: Named entities as string array 
Links: Hyperlinks as string array
You will only respond with a json document.  

Text Follows
""""""
{pageContent}
""""""")
            };
            var payload = new ApiPayload
            {
                Messages = messages,
                MaxTokens = 4000,
                Temperature = 0.7,
                TopP = 0.95,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
                Stop = null
            };
            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var response = await _chatClient.SendMessageAsync("gpt35", payload, timeout.Token);
            if (response?.Content != null)
            {
                try
                {
                    var content = response.Content;
                    //sometimes gpt returns a json malformed, a trailing comma after the links property
                    content = content.Replace("\"Links\": [],", "\"Links\": []");
                    var deserialized = JsonSerializer.Deserialize<Gpt35PageInformation>(content)!;
                    if (deserialized?.Links?.Any() == true)
                    {
                        deserialized.Links = deserialized.Links.Distinct().ToList(); //gpt tends to repeat links
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error interacting with GPT, return content is not parsable json: {content}", response.Content);
                }
            }
            return null;
        }
    }

    public class Gpt35PageInformation
    {
        public string CleanText { get; set; } = null!;
        public string Code { get; set; } = null!;
        public List<string>? Ner { get; set; }

        public List<string>? Links { get; set; }

        /// <summary>
        /// Gpt cannot clean the page.
        /// </summary>
        public bool Failed { get; set; }

        public string? ErrorMessage { get; set; }

        internal static Gpt35PageInformation? ForError(string message)
        {
            return new Gpt35PageInformation()
            {
                Failed = true,
                ErrorMessage = message,
                Ner = new List<string>(),
                Links = new List<string>(),
            };
        }

        internal bool IsValid()
        {
            return String.IsNullOrEmpty(ErrorMessage) && !string.IsNullOrEmpty(CleanText);
        }
    }
}
