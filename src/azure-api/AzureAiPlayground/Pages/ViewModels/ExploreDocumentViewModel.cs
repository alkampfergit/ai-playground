using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.Support;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Nest;
using TiktokenSharp;
using static AzureAiPlayground.Pages.ViewModels.ExploreDocumentSearchViewModel;

namespace AzureAiPlayground.Pages.ViewModels;

public class ExploreDocumentViewModel
{
    private readonly TikaOutOfProcess _tikaOutOfProcess;
    private readonly IMongoCollection<SingleDocument> _docCollection;
    private readonly IMongoCollection<SingleDocumentPage> _pagesCollection;
    private readonly TikToken _tikTokTokenizer;
    private readonly IMongoCollection<DocumentSegment> _segmentCollection;
    private readonly ElasticSearchService _esService;
    private readonly string _segmentsIndexName;

    public ExploreDocumentViewModel(
        IOptionsMonitor<DocumentsConfig> documentsConfig,
        PythonTokenizer pythonTokenizer,
        ChatClient chatClient)
    {
        var url = new MongoUrl(documentsConfig.CurrentValue.MongoUrl);
        var settings = MongoClientSettings.FromUrl(url);
        var client = new MongoClient(settings);
        var mongoDatabase = client.GetDatabase(documentsConfig.CurrentValue.DatabaseName);

        _docCollection = mongoDatabase.GetCollection<SingleDocument>("SingleDocument");
        _pagesCollection = mongoDatabase.GetCollection<SingleDocumentPage>("SingleDocumentPages");
        _segmentCollection = mongoDatabase.GetCollection<DocumentSegment>("DocumentSegments");

        _esService = new ElasticSearchService(new Uri(documentsConfig.CurrentValue.ElasticUrl));
        _segmentsIndexName = "explore-document-segments";

        SearchViewModel = new ExploreDocumentSearchViewModel(this, _segmentCollection);

        // check we can access the database reading collection 
        var count = _docCollection.CountDocuments(new BsonDocument());

        // Need to use Tika to extract document.
        _tikaOutOfProcess =
            new TikaOutOfProcess(
                documentsConfig.CurrentValue.JavaBin,
                documentsConfig.CurrentValue.Tika);

        TikToken.PBEFileDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files");
        _tikTokTokenizer = TikToken.GetEncoding("cl100k_base");
        _chatClient = chatClient;
    }

    public UiSingleDocument? CurrentDocument { get; set; }

    public DebugViewModel Logs { get; set; } = new DebugViewModel();

    public async Task Init()
    {
        await _esService.InitIndexAsync(_segmentsIndexName);
    }

    /// <summary>
    /// Uses tika to extract document metadata and document pages from a single document, it will return
    /// the original tika extracted page, containing HTML.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="docId"></param>
    /// <returns></returns>
    public async Task ExtractDocument(string path, string docId)
    {
        //try to load the document from mongo if it is not present, lets tika extract it
        //the id is docid
        var doc = _docCollection.Find(x => x.Id == docId).FirstOrDefault();
        if (doc != null)
        {
            //load all the pages from mongo
            var loadedPages = _pagesCollection.Find(x => x.SingleDocumentId == docId).ToList();
            // load all segments from mongo
            var segments = _segmentCollection.Find(x => x.SingleDocumentId == docId).ToList();
            CurrentDocument = new UiSingleDocument(doc, loadedPages, segments);
            return;
        }

        await GetDocumentFromFileWithTikaAsync(docId, path);
    }

    public async Task UpdateCountToken()
    {
        if (CurrentDocument == null) return;

        foreach (var page in CurrentDocument.Pages)
        {
            page.Page.TokenCount = _tikTokTokenizer.Encode(page.Page.Content).Count;
        }
        await SaveCurrentDocumentInMongoDb();
    }

    public async Task SegmentDocument()
    {
        if (CurrentDocument == null) return;

        //first we need to take all the pages of the current document and extract only the text
        List<string> pagesContent = new List<string>();
        foreach (var page in CurrentDocument.Pages)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(page.Page.Content);
            var content = doc.DocumentNode.InnerText;
            content = System.Web.HttpUtility.HtmlDecode(content).Trim(' ', '\r', '\n');
            pagesContent.Add(content);
        }

        //ok we need to segment document using pages content and using a certain amount of token.
        var segmenter = new Segmenter(400, 15);
        var segments = segmenter.Segment(pagesContent);
        CurrentDocument.Segments = segments
            .Select(x => new DocumentSegment(
                CurrentDocument.Document.Id,
                x.Index,
                x.Content,
                x.TokenCount))
            .Select(d => new UiSingleDocumentSegment(d))
            .ToArray();

        await SaveCurrentDocumentInMongoDb();

        //after segmentation we need to index the segments in elastic search
        var elasticDocuments = segments
            .Select(s => s.ToElasticDocument(CurrentDocument.Document.Id))
            .ToList();
        //drop all existing segments for this doc id
        await _esService.DeleteByStringPropertyAsync(_segmentsIndexName, "docid", CurrentDocument.Document.Id);
        //then index everything
        await _esService.IndexAsync(_segmentsIndexName, elasticDocuments);
    }

    private async Task SaveCurrentDocumentInMongoDb()
    {
        if (CurrentDocument == null) return;

        await SaveDocumentAsync(
            CurrentDocument.Document,
            CurrentDocument.Pages.Select(p => p.Page),
            CurrentDocument.Segments.Select(s => s.Segment));
    }

    private async Task GetDocumentFromFileWithTikaAsync(string docId, string path)
    {
        var extractedData = await _tikaOutOfProcess.GetHtmlContentAsync(path);
        if (!extractedData.Success) return; // TODO: communicate error
        var singleDocument = new SingleDocument
        {
            Id = docId,
            Metadata = new Dictionary<string, IReadOnlyCollection<string>>()
        };
        foreach (var kvp in extractedData.Metadata!)
        {
            singleDocument.Metadata.Add(kvp.Key, kvp.Value);
        }

        List<SingleDocumentPage> pages = new List<SingleDocumentPage>();
        for (var page = 0; page < extractedData.Pages!.Count; page++)
        {
            var documentPage = new SingleDocumentPage(docId, page, extractedData.Pages.ElementAt(page));
            pages.Add(documentPage);
        }

        CurrentDocument = new UiSingleDocument(singleDocument, pages, Array.Empty<DocumentSegment>());
        await SaveDocumentAsync(singleDocument, pages, null);
    }

    private async Task SaveDocumentAsync(
        SingleDocument doc,
        IEnumerable<SingleDocumentPage> pages,
        IEnumerable<DocumentSegment>? segments)
    {
        // Save the document and all the pages replacing all previous data in mongodb
        await _docCollection.ReplaceOneAsync(
            x => x.Id == doc.Id,
            doc,
            new ReplaceOptions
            {
                IsUpsert = true
            });

        // Save all the pages in a single bulk operation, first delete already existing
        // pages related to this document then save in bulk all the pages
        await _pagesCollection.DeleteManyAsync(x => x.SingleDocumentId == doc.Id);
        await _pagesCollection.InsertManyAsync(pages);

        if (segments != null && segments.Any())
        {
            await _segmentCollection.DeleteManyAsync(x => x.SingleDocumentId == doc.Id);
            await _segmentCollection.InsertManyAsync(segments);
        }
    }

    #region Keyword search

    public string? KeywordSearch { get; set; } // = "\"Complete Mediation\"";

    public string? Question { get; set; } // = "Can you please describe the technique of complete mediation?";

    public readonly ExploreDocumentSearchViewModel SearchViewModel;
    private readonly ChatClient _chatClient;

    public async Task DoKeywordSearch()
    {
        if (CurrentDocument == null) return;
        if (string.IsNullOrEmpty(KeywordSearch)) return;

        //ok we need to search
        Logs.Clear();
        await SearchViewModel.SearchKeyword(KeywordSearch, CurrentDocument.Document.Id);
    }

    public async Task DoKeywordPlusQuestionSearch()
    {
        if (CurrentDocument == null) return;
        if (string.IsNullOrEmpty(Question)) return;

        Logs.Clear();
        var keyword = string.IsNullOrEmpty(KeywordSearch) ? Question : KeywordSearch;

        Logs.AddLog("Keyword used for search", keyword);
        await SearchViewModel.SearchKeyword(keyword, CurrentDocument.Document.Id);

        //if we really have some keyword we need just to go and ask the question to the chatbot using
        //first X results as context
        var context = SearchViewModel.SegmentsQueryResults
            .Take(5)
            .Select(x => x.Content)
            .Aggregate((s1, s2) => s1 + "\"\"\"\n" + s2 + "\n\"\"\"\n");
        var chatQuestion = @$"Answer the question based only on the following context. If the context does not 
contains answer to the question you will answer ""I have not an answer"":
""""""
{context}
""""""
Question: {Question}";
        var payload = CreateBasePayload(
            "You are a chatbot that will answer questions based on a context included in the prompt. You will never user your memory to answer the question.",
            chatQuestion);

        Logs.AddLog("GPT3.5 call - question/answer", payload.Dump());
        var result = await _chatClient.SendMessageAsync("gpt35", payload);
        Logs.AddLog("GPT3.5 result - question/answer", result.Dump());
    }

    public async Task DoKeywordPlusQuestionSearchWithDocumentExpansion()
    {
        if (CurrentDocument == null) return;
        if (string.IsNullOrEmpty(Question)) return;

        Logs.Clear();
        //Now we need to perform a double step operation.
        if (string.IsNullOrEmpty(KeywordSearch))
        {
            Logs.AddLog("User does not specify keyword.", "");
            await PerformKeywordSearchFromQuestion(Question);
        }
        else
        {
            Logs.AddLog("User specified keyword to use for search", KeywordSearch);
            await SearchViewModel.SearchKeyword(KeywordSearch, CurrentDocument.Document.Id);
        }

        //if we really have some keyword we need just to go and ask the question to the chatbot using
        //first X results as context
        var context = SearchViewModel.SegmentsQueryResults
            .Take(5)
            .Select(x => x.Content)
            .Aggregate((s1, s2) => s1 + "\"\"\"\n" + s2 + "\n\"\"\"\n");
        var chatQuestion = @$"Answer the question based only on the following context. If the context does not 
contains answer to the question you will answer ""I have not an answer"":
""""""
{context}
""""""
Question: {Question}";
        var payload = CreateBasePayload(
            "You are a chatbot that will answer questions based on a context included in the prompt. You will never user your memory to answer the question.",
            chatQuestion);

        Logs.AddLog("GPT3.5 call - question/answer", payload.Dump());
        var result = await _chatClient.SendMessageAsync("gpt35", payload);
        Logs.AddLog("GPT3.5 result - question/answer", result.Dump());
    }

    private async Task PerformKeywordSearchFromQuestion(string question)
    {
        const string systemMessage = "You are a search assistant expert in Searching into lucene";
        string prompt = $@"You will extract space separated keywords from a question made by the user.
question: {question}:
keywords: ";

        var payload = CreateBasePayload(systemMessage, prompt);

        Logs.AddLog("GPT3.5 call - keyword search from question", payload.Dump());
        var result = await _chatClient.SendMessageAsync("gpt35", payload);
        Logs.AddLog("GPT3.5 result - keyword search from question", result.Dump());

        //now that I have keyword I can proceed with the search.
        var queries = new[]
        {
            new BoostedQuery(question, 1),
            new BoostedQuery(result.Content, 10)
        };
        await SearchViewModel.SearchKeyword(queries, CurrentDocument!.Document.Id);
    }

    #endregion

    #region Helper

    private static ApiPayload CreateBasePayload(
        string systemMessage,
        string chatQuestion)
    {
        return new ApiPayload
        {
            Messages = new List<Message>
            {
                new Message { Role = "system", Content = systemMessage },
                new Message { Role = "user", Content = chatQuestion }
            },
            MaxTokens = 500,
            Temperature = 0.2,
            FrequencyPenalty = 1,
            PresencePenalty = 2,
            TopP = 0.9,
            Stop = null
        };
    }

    #endregion
}

public class UiSingleDocument
{
    public UiSingleDocument(
        SingleDocument document,
        IEnumerable<SingleDocumentPage> pages,
        IEnumerable<DocumentSegment> segments)
    {
        Document = document;
        Pages = pages
            .OrderBy(p => p.PageNumber)
            .Select(p => new UiSingleDocumentPage(p))
            .ToArray();

        Segments = segments
            .OrderBy(p => p.PageNumber)
            .Select(p => new UiSingleDocumentSegment(p))
            .ToArray();
    }

    public UiSingleDocumentSegment[] Segments { get; set; }

    public SingleDocument Document { get; set; }

    public IReadOnlyCollection<UiSingleDocumentPage> Pages { get; set; }
}

public class UiSingleDocumentPage
{
    public UiSingleDocumentPage(SingleDocumentPage page)
    {
        Page = page;
    }

    public bool ShowDetails { get; set; } = false;

    public SingleDocumentPage Page { get; set; }
}

public class UiSingleDocumentSegment
{
    public UiSingleDocumentSegment(DocumentSegment segment)
    {
        Segment = segment;
    }

    public bool ShowDetails { get; set; } = false;

    public DocumentSegment Segment { get; set; }
}

/// <summary>
/// Represents a single document where we want to perform various analysis.
/// </summary>
public class SingleDocument
{
    /// <summary>
    /// Id is assigned from the user and is used to uniquely identify the file.
    /// </summary>
    public string Id { get; set; } = null!;

    public required Dictionary<string, IReadOnlyCollection<string>> Metadata { get; set; }
}

/// <summary>
/// represent a single page of a document.
/// </summary>
public class SingleDocumentPage
{
    public SingleDocumentPage(string singleDocumentId, int pageNumber, string content)
    {
        SingleDocumentId = singleDocumentId;
        PageNumber = pageNumber;
        Content = content;
        Id = ObjectId.GenerateNewId();
    }

    public ObjectId Id { get; set; }

    public int PageNumber { get; set; }
    public string SingleDocumentId { get; set; }

    public string Content { get; set; }

    public int? TokenCount { get; set; }
}

/// <summary>
/// Subdividing text in pages as for Tika extractino is not smart ,it is much
/// better to divide the text in chunks
/// </summary>
public class DocumentSegment
{
    public DocumentSegment(string singleDocumentId, int pageNumber, string content, int tokenCount)
    {
        Id = ObjectId.GenerateNewId();
        SingleDocumentId = singleDocumentId;
        PageNumber = pageNumber;
        Content = content;
        TokenCount = tokenCount;
    }

    public ObjectId Id { get; set; }

    /// <summary>
    /// We can maintain the information of the page the segment belongs to
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Clearly we need to reference the document
    /// </summary>
    public string SingleDocumentId { get; set; }

    public string Content { get; set; }

    public int? TokenCount { get; set; }
}