using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.DocumentChat;
using Nest;

namespace AzureAiLibrary.Tests.Documents;

public class ElasticSearchServiceSegmentQueryTests : IDisposable, IAsyncLifetime
{
    private readonly ElasticSearchService _sut;
    private readonly ElasticClient _elasticClient;
    private readonly string _indexName;
    private readonly Random _random;

    public ElasticSearchServiceSegmentQueryTests()
    {
        Uri uri = new Uri("http://localhost:9200");
        _sut = new ElasticSearchService(uri);
        _elasticClient = new ElasticClient(uri);

        _indexName = "test_" + Guid.NewGuid().ToString().Replace("-", "");
        _random = new Random((int)DateTime.Now.Ticks);
   }

    public async Task InitializeAsync()
    {
        await _sut.InitIndexAsync(_indexName);
        await IndexData();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private async Task IndexData()
    {
        List<ElasticDocumentSegment> segments = new List<ElasticDocumentSegment>();
        segments.Add(new ElasticDocumentSegment("doc1", "This is a beautiful test", 1) { Tag = "file1"});
        segments.Add(new ElasticDocumentSegment("doc1", "Some interesting content", 2) { Tag = "file1"});
        segments.Add(new ElasticDocumentSegment("doc1", "We can talk about complete mediation", 1) { Tag = "file2"});
        segments.Add(new ElasticDocumentSegment("doc1", "We could index some data and try to retrieve with some interesting data", 1) { Tag = "file2"});
        segments.Add(new ElasticDocumentSegment("doc2", "Oh oh oh, this is Christmas", 1));

        //We cannot do async in constructor.
        var indexed = await _sut.IndexAsync(_indexName, segments);
        Assert.True(indexed);
        await _sut.Refresh(_indexName);
    }

    public void Dispose()
    {
        _elasticClient.Indices.Delete(_indexName + "*");
    }

    [Fact]
    public async Task Basic_test()
    {
        var segmentSearch = new SegmentsSearch(_indexName);
        segmentSearch.DocId = new string[] { "doc1" };
        var result = await _sut.SearchSegmentsAsync(segmentSearch);
        Assert.Equal(4, result.Count);
    }
}
