using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.DocumentChat;
using Nest;

namespace AzureAiLibrary.Tests.Documents;

public class ElasticSearchServiceSegmentQueryTests : IDisposable, IAsyncLifetime
{
    private readonly ElasticSearchService _sut;
    private readonly ElasticClient _elasticClient;
    private readonly string _indexName;
    private List<ElasticDocumentSegment>? _segments;

    public ElasticSearchServiceSegmentQueryTests()
    {
        Uri uri = new Uri("http://localhost:9200");
        _sut = new ElasticSearchService(uri);
        _elasticClient = new ElasticClient(uri);

        _indexName = "test_" + Guid.NewGuid().ToString().Replace("-", "");
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
        _segments = new List<ElasticDocumentSegment>();
        _segments.Add(new ElasticDocumentSegment("doc1", "This is a beautiful test", 1) { Tag = "file1" });
        _segments.Add(new ElasticDocumentSegment("doc1", "Some interesting content", 2) { Tag = "file1" });
        _segments.Add(new ElasticDocumentSegment("doc1", "We can talk about complete mediation", 1) { Tag = "file2" });
        _segments.Add(new ElasticDocumentSegment("doc1", "We could index some data and try to retrieve with some interesting data", 1) { Tag = "file2" });
        _segments.Add(new ElasticDocumentSegment("doc2", "Oh oh oh, this is Christmas", 1));

        //We cannot do async in constructor.
        var indexed = await _sut.IndexAsync(_indexName, _segments);
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

    [Fact]
    public async Task Keyword_search()
    {
        var segmentSearch = new SegmentsSearch(_indexName);
        segmentSearch.DocId = new string[] { "doc1" };
        segmentSearch.Keywords = "interesting";
        var result = await _sut.SearchSegmentsAsync(segmentSearch);
        Assert.Equal(2, result.Count);

        Assert.Contains(result, r => r.Content == "Some interesting content");
        Assert.Contains(result, r => r.Content == "We could index some data and try to retrieve with some interesting data");
    }

    [Fact]
    public async Task Delete_and_reindex_entire_document() 
    {
        //save some document data then delete all document by document id
        var segments = new List<ElasticDocumentSegment>();
        segments.Add(new ElasticDocumentSegment("doc3", "page 1", 1) { Tag = "file1" });
        segments.Add(new ElasticDocumentSegment("doc3", "page 2", 2) { Tag = "file1" });
        segments.Add(new ElasticDocumentSegment("doc3", "page 1", 1) { Tag = "file3" });

        //We cannot do async in constructor.
        var indexed = await _sut.IndexAsync(_indexName, _segments);
        Assert.True(indexed);
        await _sut.Refresh(_indexName);

        //ACT: delete the document and reindex with less data
        var segmentSearch = new SegmentsSearch(_indexName);
        segmentSearch.DocId = new string[] { "doc3" };
        await _sut.DeleteSegmentsByQueryAsync(segmentSearch);
        await _sut.Refresh(_indexName);

        //remove last element from segments and reindex everything
        segments.RemoveAt(segments.Count - 1);
        indexed = await _sut.IndexAsync(_indexName, segments);
        Assert.True(indexed);
        await _sut.Refresh(_indexName);

        //ASSERT: check that we have only 2 segments
        segmentSearch = new SegmentsSearch(_indexName);
        segmentSearch.DocId = new string[] { "doc3" };
        var result = await _sut.SearchSegmentsAsync(segmentSearch);
        Assert.Equal(2, result.Count);
    }
}
