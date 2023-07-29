using MongoDB.Driver;
using Serilog;
using System.Text;
using System.Text.Json;

namespace AzureAiLibrary.Documents.Jobs
{
    /// <summary>
    /// A text cleaner that uses AI to clean the text of the pages extracted, this will actually
    /// improve the quality of the text using AI
    /// </summary>
    public class Gpt35AiCleaner : BaseJob
    {
        private readonly ChatClient _chatClient;

        private ILogger _logger = Log.ForContext<Gpt35AiCleaner>();

        public Gpt35AiCleaner(IMongoDatabase db, ChatClient chatClient) : base(db)
        {
            _chatClient = chatClient;
        }

        protected override string PollerProperty => "CleanWithGpt35";

        protected override async Task InnerPerformTask(MongoDocumentToIndex rawDocument)
        {
            var totalPages = rawDocument.Pages.Count(p => !p.Removed);
            foreach (var page in rawDocument.Pages.Where(p => !p.Removed))
            {
                if (page.Gpt35PageInformation != null) continue;

                try
                {
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
                    Logger.Information("About to clean page {pageNum} with GPT35 on a total of {total}", page.Number, totalPages);
                    page.Gpt35PageInformation = await CleanPage(text.ToString());
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error cleaning text with GPT35 in page {pageNum}", page.Number);
                    rawDocument.CleanWithGpt35Errors = $"Error cleaning text with GPT35 in page {page.Number}: {ex}";
                    return; //exit
                }
            }

            Logger.Information("Finished cleanning {docId} with GPT3.5", rawDocument.Id);
        }

        public async Task<Gpt35PageInformation> CleanPage(string pageContent)
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
            if (response != null)
            {
                try
                {
                    return JsonSerializer.Deserialize<Gpt35PageInformation>(response.Content)!;
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
    }
}
