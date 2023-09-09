using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.Jobs;
using AzureAiLibrary.Documents.Support;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using static AzureAiLibrary.Documents.ElasticSearchService;

namespace AzureAiPlayground.Pages.ViewModels;

public class ExploreDocumentViewModel
{
    private readonly Gpt35AiCleaner _gpt35AiCleaner;
    private readonly ElasticSearchService _elasticSearchService;
    private readonly ElasticSearchIndexerJob _elasticIndexer;
    private readonly PythonTokenizer _pythonTokenizer;
    private IMongoDatabase _mongoDatabase;
    private TikaOutOfProcess _tikaOutOfProcess;
    private RawDocumentSimpleReader _rawDocumentSimpleReader;

    public ExploreDocumentViewModel(
        IOptionsMonitor<DocumentsConfig> documentsConfig,
        PythonTokenizer pythonTokenizer,
        ChatClient chatClient)
    {
        var url = new MongoUrl(documentsConfig.CurrentValue.MongoUrl);
        var settings = MongoClientSettings.FromUrl(url);
        var client = new MongoClient(settings);
        _mongoDatabase = client.GetDatabase("DocSampleV1");
        // Need to use Tika to extract document.
        _tikaOutOfProcess = 
            new TikaOutOfProcess(
                documentsConfig.CurrentValue.JavaBin,
                documentsConfig.CurrentValue.Tika);

        //TODO: Register database and register this in the ui container.
        _rawDocumentSimpleReader = new RawDocumentSimpleReader(_mongoDatabase);

        //TODO register all jobs and resolve all
        _gpt35AiCleaner = new Gpt35AiCleaner(_mongoDatabase, chatClient);
        _elasticSearchService = new ElasticSearchService(new Uri(documentsConfig.CurrentValue.ElasticUrl));
        _elasticIndexer = new ElasticSearchIndexerJob(_mongoDatabase, _elasticSearchService);
        _pythonTokenizer = pythonTokenizer;
    }
    
    /// <summary>
    /// Uses tika to extract document metadata and document pages from a single document, it will return
    /// the original tika extracted page, containing HTML.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="docId"></param>
    /// <returns></returns>
    public async Task<UiSingleDocument> ExtractDocument(string path, string docId)
    {
        var extractedData = await _tikaOutOfProcess.GetHtmlContentAsync(path);
        var singleDocument = new SingleDocument
        {
            Id = docId,
            Metadata = new Dictionary<string, IReadOnlyCollection<string>>()
        };
        foreach (var kvp in extractedData.Metadata)
        {
            singleDocument.Metadata.Add(kvp.Key, kvp.Value);
        }
        List<SingleDocumentPage> pages = new List<SingleDocumentPage>();
        for (var page = 0; page < extractedData.Pages.Count; page++)
        {
            var documentPage = new SingleDocumentPage(docId,page, extractedData.Pages.ElementAt(page));
            pages.Add(documentPage);
        }
        return new UiSingleDocument(singleDocument, pages);
    }
}

public class UiSingleDocument
{
    public UiSingleDocument(SingleDocument document, IReadOnlyCollection<SingleDocumentPage> pages)
    {
        Document = document;
        Pages = pages;
    }

    public SingleDocument Document { get; set; }
    
    public IReadOnlyCollection<SingleDocumentPage> Pages { get; set; }
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
    public SingleDocumentPage( string singleDocumentId, int pageNumber, string content)
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
}