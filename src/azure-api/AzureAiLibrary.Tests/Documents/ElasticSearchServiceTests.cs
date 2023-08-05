using AzureAiLibrary.Documents;
using Nest;

namespace AzureAiLibrary.Tests.Documents;

public class ElasticSearchServiceTests : IDisposable
{
    private ElasticSearchService _sut;
    private ElasticClient _elasticClient;
    private string _indexName;
    private Random _random;

    public ElasticSearchServiceTests()
    {
        Uri uri = new Uri("http://localhost:9200");
        _sut = new ElasticSearchService(uri);
        _elasticClient = new ElasticClient(uri);

        _indexName = "test_" + Guid.NewGuid().ToString().Replace("-", "");
        _random = new Random((int)DateTime.Now.Ticks);
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
    public async Task When_add_dense_vector_mapping_is_created()
    {
        var (vectorName, _, _) = await CreateIndexAndIndexDataWithVectors();

        //Assert, we need to have a mapping for the dense index.
        var request = new GetMappingRequest(_indexName);
        var mappings = _elasticClient.Indices.GetMapping(request);

        //Verify mapping
        AssertDenseVectorMapping(vectorName, mappings, "vector", 512);
        AssertDenseVectorMapping(vectorName, mappings, "normalized_vector", 512);
        AssertDenseVectorMapping(vectorName, mappings, "gpt35_vector", 512);
        AssertDenseVectorMapping(vectorName, mappings, "gpt35_normalized_vector", 512);

        Assert.True(mappings.IsValid);
    }

    [Fact]
    public async Task When_add_dense_vector_data_is_created()
    {
        var (vectorName, dataId, vectorData) = await CreateIndexAndIndexDataWithVectors();

        //Assert: retrive the data and verify vectors are there
        ElasticDocument? reloaded = await _sut.GetByIdAsync(_indexName, dataId);
        Assert.NotNull(reloaded);

        //ok we need now to access the vectors.
        var vector = reloaded.GetVector(vectorName);
        Assert.NotNull(vector);
        Assert.IsType<SingleDenseVectorData>(vector);
        var v = (SingleDenseVectorData)vector;
        Assert.Equal(vectorData.vectorData, v.vectorData);
        Assert.Equal(vectorData.normalizedVectorData, v.normalizedVectorData);
        Assert.Equal(vectorData.gpt35VectorData, v.gpt35VectorData);
        Assert.Equal(vectorData.gpt35NormalizedVectorData, v.gpt35NormalizedVectorData);
    }

    private void AssertDenseVectorMapping(string vectorName, GetMappingResponse mappings, string suffix, int expectedDimension)
    {
        var denseVector = mappings.Indices[_indexName].Mappings.Properties[$"v_{vectorName}_{suffix}"];
        Assert.NotNull(denseVector);
        Assert.Equal("dense_vector", denseVector.Type);
        Assert.IsType<DenseVectorProperty>(denseVector);

        var denseVectorProperty = (DenseVectorProperty)denseVector;
        Assert.Equal(expectedDimension, denseVectorProperty.Dimensions);
    }

    [Fact]
    public async Task Can_index_a_dynamic()
    {
        await _sut.InitIndexAsync(_indexName);

        ElasticDocument data = new(Guid.NewGuid().ToString());
        data.Title = "Test Title";
        data["s_metadata1"] = "BLAAAA";
        data["s_metadata2"] = "this is a test";

        var insert = await _sut.IndexAsync(_indexName, new ElasticDocument[] { data });
        Assert.True(insert, "Bulk insert did not work");

        ElasticDocument? reloaded = await _sut.GetByIdAsync(_indexName, data.Id);

        Assert.NotNull(reloaded);

        //Assert equality
        Assert.Equal(data.Id, reloaded.Id);

        Assert.Equal(data.Title, reloaded.Title);
        Assert.Equal(data["s_metadata1"], reloaded["s_metadata1"]);
        Assert.Equal(data["s_metadata2"], reloaded["s_metadata2"]);
    }

    private async Task<(string vectorFieldName, string documentId, SingleDenseVectorData vectorData)> CreateIndexAndIndexDataWithVectors()
    {
        //when the service is inited it should cretate a mapping
        await _sut.InitIndexAsync(_indexName);

        //Arrange, save a document
        ElasticDocument data = new(Guid.NewGuid().ToString());
        data.Title = "Test Title";
        data["s_metadata1"] = "BLAAAA";
        data["s_metadata2"] = "this is a test";

        var insert = await _sut.IndexAsync(_indexName, new ElasticDocument[] { data });
        Assert.True(insert, "Bulk insert did not work");

        //Act: add a new dense vector.
        var vectorName = "test_vector_" + Guid.NewGuid().ToString().Replace("-", "");

        SingleDenseVectorData singleDenseVectorData = new SingleDenseVectorData(
                    data.Id,
                    vectorName,
                    vectorData: GenerateRandomVector(512),
                    normalizedVectorData: GenerateRandomVector(512),
                    gpt35NormalizedVectorData: GenerateRandomVector(512),
                    gpt35VectorData: GenerateRandomVector(512));
        SingleDenseVectorData[] vectorList = new SingleDenseVectorData[] { singleDenseVectorData };
        await _sut.IndexDenseVectorAsync(_indexName, vectorList);
        return (vectorName, data.Id, singleDenseVectorData);
    }

    private double[] GenerateRandomVector(int dimension)
    {
        //Generate an array of dimension with random number
        double[] retValue = new double[dimension];
        for (int i = 0; i < dimension; i++)
        {
            retValue[i] = _random.Next() / (double)_random.Next();
        }

        return retValue;
    }
}
