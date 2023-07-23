using Nest;

public class ElasticDocument
{
    public string Id { get; set; } = null!;

    [Text(Name = "Title")]
    public string Title { get; set; }

    [Number(Name = "PageNum")]
    public int PageNum { get; set; }

    [Text]
    public string Text {get; set;}
}

public class ElasticSearchService
{
    private readonly ElasticClient _elasticClient;

    public ElasticSearchService(Uri uri)
    {
        var settings = new ConnectionSettings(uri);
        _elasticClient = new ElasticClient(settings);
    }

    public bool IndexExists(string indexName)
    {
        var response = _elasticClient.Indices.Exists(indexName);
        return response.Exists;
    }

    public void CreateIndex(string indexName)
    {
        var createIndexResponse = _elasticClient.Indices.Create(indexName, c => c
            .Map<ElasticDocument>(m => m.AutoMap()));
    }

    public void VerifyOrCreateIndex(string indexName)
    {
        if (!IndexExists(indexName))
        {
            CreateIndex(indexName);
        }
    }
}