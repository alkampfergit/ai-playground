using Nest;

namespace AzureAiLibrary.Tests.Documents;

public class ElasticSearchServiceTests : IDisposable
{
    private ElasticSearchService _sut;
    private ElasticClient _elasticClient;
    private string _indexName;

    public ElasticSearchServiceTests()
    {
        Uri uri = new Uri("http://localhost:9201");
        _sut = new ElasticSearchService(uri);
        _elasticClient = new ElasticClient(uri);

        _indexName = "test_" + Guid.NewGuid().ToString().Replace("-", "");
    }

    public void Dispose()
    {
        _elasticClient.Indices.Delete(_indexName + "*");
    }

    [Fact]
    public async Task Verify_a_mapping_exists()
    {
        //when the service is inited it should cretate a mapping
        await _sut.InitIndexAsync(_indexName);

        //we need to have a mapping for the index.
        var request = new GetMappingRequest(_indexName);
        var mappings = _elasticClient.Indices.GetMapping(request);

        Assert.True(mappings.IsValid);
    }

    [Fact]
    public async Task Can_index_a_dynamic()
    {
        await _sut.InitIndexAsync(_indexName);

        ElasticDocument data = new(Guid.NewGuid().ToString());
        data["Title"] = "Test Title";
        data["s_metadata1"] = "BLAAAA";
        data["s_metadata2"] = "this is a test";

        var insert = await _sut.IndexAsync(_indexName, new ElasticDocument[] { data });
        Assert.True(insert, "Bulk insert did not work");

        ElasticDocument? reloaded = await _sut.GetByIdAsync(_indexName, data.Id);

        Assert.NotNull(reloaded);

        //Assert equality
        Assert.Equal(data.Id, reloaded.Id);

        Assert.Equal(data["Title"], reloaded["Title"]);
        Assert.Equal(data["s_metadata1"], reloaded["s_metadata1"]);
        Assert.Equal(data["s_metadata2"], reloaded["s_metadata2"]);
    }
}
