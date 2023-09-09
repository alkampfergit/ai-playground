using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.Jobs;
using AzureAiLibrary.Documents.Support;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using static AzureAiLibrary.Documents.ElasticSearchService;

namespace AzureAiPlayground.Pages.ViewModels;

public class DocumentsViewModel
{
    private readonly Gpt35AiCleaner _gpt35AiCleaner;
    private readonly ElasticSearchService _elasticSearchService;
    private readonly ElasticSearchIndexerJob _elasticIndexer;
    private readonly PythonTokenizer _pythonTokenizer;
    private IMongoDatabase _mongoDatabase;
    private TikaExtractor _tikaExtractor;
    private RawDocumentSimpleReader _rawDocumentSimpleReader;

    public DocumentsViewModel(
        IOptionsMonitor<DocumentsConfig> documentsConfig,
        PythonTokenizer pythonTokenizer,
        ChatClient chatClient)
    {
        var url = new MongoUrl(documentsConfig.CurrentValue.MongoUrl);
        var settings = MongoClientSettings.FromUrl(url);
        var client = new MongoClient(settings);
        _mongoDatabase = client.GetDatabase(url.DatabaseName);
        _tikaExtractor = new TikaExtractor(
            new TikaOutOfProcess(
                documentsConfig.CurrentValue.JavaBin,
                documentsConfig.CurrentValue.Tika),
            _mongoDatabase);

        //TODO: Register database and register this in the ui container.
        _rawDocumentSimpleReader = new RawDocumentSimpleReader(_mongoDatabase);

        //TODO register all jobs and resolve all
        _gpt35AiCleaner = new Gpt35AiCleaner(_mongoDatabase, chatClient);
        _elasticSearchService = new ElasticSearchService(new Uri(documentsConfig.CurrentValue.ElasticUrl));
        _elasticIndexer = new ElasticSearchIndexerJob(_mongoDatabase, _elasticSearchService);
        _pythonTokenizer = pythonTokenizer;
    }

    public Task ExtractPath(string path, string filter)
    {
        return _tikaExtractor.Extract(path, filter);
    }

    internal async Task StartServices()
    {
        _rawDocumentSimpleReader.Start();
        await _gpt35AiCleaner.Start();
        await _elasticIndexer.Start();
    }

    internal Task QueueEmbeddingInPython(string model)
    {
        return Task.CompletedTask;
    }

    public async Task<IList<DocumentSearchResult>> SearchKeywordAsync(string querystring)
    {
        QueryDefinition query = new(ElasticSearchIndexerJob.IndexName, querystring);
        //Original content should be weighted higher than the gpt35 content?
        query.FieldsDefinition.Add(new FieldsDefinition("content", 5));
        query.FieldsDefinition.Add(new FieldsDefinition("gpt35content", 1));

        var result = await _elasticSearchService.SearchAsync(query);
        return result?.Select(r => new DocumentSearchResult(r)).ToList() ?? new List<DocumentSearchResult>();
    }

    public async Task<IList<DocumentSearchResult>> SearchVectorAsync(string querystring)
    {
        var tokenizedVector = await _pythonTokenizer.Tokenize(querystring, "Bert");
        QueryDefinition query = new(ElasticSearchIndexerJob.IndexName);
        query.VectorSearches.Add(new VectorSearch("Bert", tokenizedVector, false));

        var result = await _elasticSearchService.SearchAsync(query);
        return result?.Select(r => new DocumentSearchResult(r)).ToList() ?? new List<DocumentSearchResult>();
    }
}

public class DocumentSearchResult
{
    public DocumentSearchResult(ElasticDocument document)
    {
        var id = document.GetStringProperty("docid") ?? document.Id;
        Id = id.Split('/', '\\').LastOrDefault() ?? id;
        PageNumber = Convert.ToInt32(document.GetNumericProperty("page"));
        DocTitle = document.GetStringProperty("title") ?? "no title";
        Content = document.GetTextProperty("content") ?? "no content";
        Gpt35Content = document.GetTextProperty("gpt35content");
    }

    public string Id { get; set; } = null!;
    public int PageNumber { get; set; }

    public string DocTitle { get; set; } = null!;
    public string Content { get; set; }
    public string? Gpt35Content { get; set; }

    /// <summary>
    /// UI properties to tells the ui if you need to show the details.
    /// </summary>
    public bool ShowDetails { get; internal set; }
}