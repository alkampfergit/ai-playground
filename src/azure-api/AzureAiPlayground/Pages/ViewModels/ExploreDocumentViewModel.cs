using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.Support;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using TiktokenSharp;

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
        var mongoDatabase = client.GetDatabase("DocSampleV1");

        _docCollection = mongoDatabase.GetCollection<SingleDocument>("SingleDocument");
        _pagesCollection = mongoDatabase.GetCollection<SingleDocumentPage>("SingleDocumentPages");
        _segmentCollection = mongoDatabase.GetCollection<DocumentSegment>("DocumentSegments");

        _esService = new ElasticSearchService(new Uri(documentsConfig.CurrentValue.ElasticUrl));
        _segmentsIndexName = "explore-document-segments";

        // check we can access the database reading collection 
        var count = _docCollection.CountDocuments(new BsonDocument());

        // Need to use Tika to extract document.
        _tikaOutOfProcess =
            new TikaOutOfProcess(
                documentsConfig.CurrentValue.JavaBin,
                documentsConfig.CurrentValue.Tika);

        TikToken.PBEFileDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files");
        _tikTokTokenizer = TikToken.GetEncoding("cl100k_base");
    }

    public UiSingleDocument? CurrentDocument { get; set; }

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

        if (segments != null)
        {
            await _segmentCollection.DeleteManyAsync(x => x.SingleDocumentId == doc.Id);
            await _segmentCollection.InsertManyAsync(segments);
        }
    }

    #region Keyword search

    public string? KeywordSearch { get; set; }

    public ExploreDocumentSearchViewModel SearchViewModel = new ExploreDocumentSearchViewModel();
    public async Task DoKeywordSearch()
    {
        if (string.IsNullOrEmpty(KeywordSearch)) return;

        //ok we need to search
        SearchViewModel.Clear();

        await SearchInAtlas();
        //await SearchInElasticsearch();
    }

    private async Task SearchInAtlas()
    {
        var searchStage = new BsonDocument
        {
            {
                "$search", new BsonDocument
                {
                    { "index", "segments" },
                    {
                        "queryString", new BsonDocument
                        {
                            { "query", KeywordSearch },
                            { "defaultPath", "Content" }
                        }
                    },
                    {
                        "highlight", new BsonDocument
                        {
                            { "path", "Content" }
                        }
                    },
                    {
                        "scoreDetails" , true
                    }
                }
            }
        };

        var projectStage = new BsonDocument
        {
            {
              "$project", new BsonDocument {
                { "description", 1 },
                { "_id", 0 },
                { "PageNumber", 1 },
                { "Content", 1 },
                { "SingleDocumentId", 1 },
                { "highlights", new BsonDocument("$meta", "searchHighlights") } }
              }
        };

        var pipeline = new List<BsonDocument> { searchStage, projectStage };
        var resultAggregateAsync = await _segmentCollection.AggregateAsync<BsonDocument>(pipeline);

        var result = await resultAggregateAsync.ToListAsync();

        foreach (var doc in result)
        {
            System.Console.WriteLine(doc.ToJson());
        }

        //SearchViewModel.SegmentsQueryResults = result
        //    .Select(x => (
        //        Segmenter.SegmentInfo.FromElasticDocument(x),
        //        x.GetStringProperty("docid") ?? ""))
        //    .Select(d => new DocumentSegment(d.Item2, d.Item1.Index, d.Item1.Content, d.Item1.TokenCount))
        //    .Select(d => new UiSingleDocumentSegment(d))
        //    .ToList();
    }

    private async Task SearchInElasticsearch()
    {
        //simple elastic search query
        var result = await _esService.SearchAsync(
            _segmentsIndexName,
            new[] { "content" },
            KeywordSearch);

        SearchViewModel.SegmentsQueryResults = result
            .Select(x => (
                Segmenter.SegmentInfo.FromElasticDocument(x),
                x.GetStringProperty("docid") ?? ""))
            .Select(d => new DocumentSegment(d.Item2, d.Item1.Index, d.Item1.Content, d.Item1.TokenCount))
            .Select(d => new UiSingleDocumentSegment(d))
            .ToList();
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