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
    public async Task Can_init_more_than_once()
    {
        //when the service is inited it should cretate a mapping
        await _sut.InitIndexAsync(_indexName);
        await _sut.InitIndexAsync(_indexName);
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

    /// <summary>
    /// Properties can be stored as array of strings, there is no problem because elastic
    /// does not distinguish between single value and array of values.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Index_text_properties_as_array()
    {
        await _sut.InitIndexAsync(_indexName);

        ElasticDocument data = new (Guid.NewGuid().ToString());
        data.Title = "Test Title";
        data.AddStringProperty("ner", new[] { "api", "chatgp", "ai"});

        var insert = await _sut.IndexAsync(_indexName, new ElasticDocument[] { data });
        Assert.True(insert, "Bulk insert did not work");

        ElasticDocument? reloaded = await _sut.GetByIdAsync(_indexName, data.Id);

        Assert.NotNull(reloaded);

        //Assert equality
        Assert.Equal(data.Id, reloaded.Id);

        Assert.Equal(data.Title, reloaded.Title);
        Assert.Equal(data["s_ner"], reloaded["s_ner"]);
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
        Assert.Equal(vectorData.VectorData, v.VectorData);
        Assert.Equal(vectorData.NormalizedVectorData, v.NormalizedVectorData);
        Assert.Equal(vectorData.Gpt35VectorData, v.Gpt35VectorData);
        Assert.Equal(vectorData.Gpt35NormalizedVectorData, v.Gpt35NormalizedVectorData);
    }

    [Fact]
    public async Task Can_add_two_types_of_vector_in_a_single_call() 
    {
        //when the service is inited it should cretate a mapping
        await _sut.InitIndexAsync(_indexName);

        //Arrange, save a document
        await _sut.InitIndexAsync(_indexName);

        //Arrange, save a document
        ElasticDocument data = await SaveADocument();

        //Act: add a new dense vector.
        var vectorName1 = "test_vector_" + Guid.NewGuid().ToString().Replace("-", "");
        var vectorName2 = "test_vector_" + Guid.NewGuid().ToString().Replace("-", "");

        //In a single call we want to index two vector data.
        SingleDenseVectorData singleDenseVectorData1 = CreateSingleDenseVectorData(data, vectorName1);
        SingleDenseVectorData singleDenseVectorData2 = CreateSingleDenseVectorData(data, vectorName2);

        SingleDenseVectorData[] vectorList = new SingleDenseVectorData[] { singleDenseVectorData1, singleDenseVectorData2 };
        await _sut.IndexDenseVectorAsync(_indexName, vectorList);

        //Assert: retrive the data and verify vectors are there
        ElasticDocument? reloaded = await _sut.GetByIdAsync(_indexName, data.Id);
        Assert.NotNull(reloaded);

        //ok we need now to access the vectors.
        var vector1 = reloaded.GetVector(vectorName1);
        Assert.NotNull(vector1);
        Assert.IsType<SingleDenseVectorData>(vector1);
        Assert.Equal(singleDenseVectorData1.VectorData, vector1.VectorData);
        Assert.Equal(singleDenseVectorData1.NormalizedVectorData, vector1.NormalizedVectorData);
        Assert.Equal(singleDenseVectorData1.Gpt35VectorData, vector1.Gpt35VectorData);
        Assert.Equal(singleDenseVectorData1.Gpt35NormalizedVectorData, vector1.Gpt35NormalizedVectorData);

        var vector2 = reloaded.GetVector(vectorName2);
        Assert.NotNull(vector2);
        Assert.IsType<SingleDenseVectorData>(vector2 );
        Assert.Equal(singleDenseVectorData2.VectorData, vector2.VectorData);
        Assert.Equal(singleDenseVectorData2.NormalizedVectorData, vector2.NormalizedVectorData);
        Assert.Equal(singleDenseVectorData2.Gpt35VectorData, vector2.Gpt35VectorData);
        Assert.Equal(singleDenseVectorData2.Gpt35NormalizedVectorData, vector2.Gpt35NormalizedVectorData);
    }

    [Fact]
    public async Task Can_add_two_types_of_vector_in_a_subsequent_call()
    {
        //when the service is inited it should cretate a mapping
        await _sut.InitIndexAsync(_indexName);

        //Arrange, save a document
        await _sut.InitIndexAsync(_indexName);

        //Arrange, save a document
        ElasticDocument data = await SaveADocument();

        //Act: add a new dense vector.
        var vectorName1 = "test_vector_" + Guid.NewGuid().ToString().Replace("-", "");
        var vectorName2 = "test_vector_" + Guid.NewGuid().ToString().Replace("-", "");

        //In a single call we want to index two vector data.
        SingleDenseVectorData singleDenseVectorData1 = CreateSingleDenseVectorData(data, vectorName1);
        SingleDenseVectorData singleDenseVectorData2 = CreateSingleDenseVectorData(data, vectorName2);

        //Index first a vector then another vector
        SingleDenseVectorData[] vectorList1 = new SingleDenseVectorData[] { singleDenseVectorData1,  };
        await _sut.IndexDenseVectorAsync(_indexName, vectorList1);
        SingleDenseVectorData[] vectorList2 = new SingleDenseVectorData[] { singleDenseVectorData2 };
        await _sut.IndexDenseVectorAsync(_indexName, vectorList2);

        //Assert: retrive the data and verify vectors are there
        ElasticDocument? reloaded = await _sut.GetByIdAsync(_indexName, data.Id);
        Assert.NotNull(reloaded);

        //ok we need now to access the vectors.
        var vector1 = reloaded.GetVector(vectorName1);
        Assert.NotNull(vector1);
        Assert.IsType<SingleDenseVectorData>(vector1);
        Assert.Equal(singleDenseVectorData1.VectorData, vector1.VectorData);
        Assert.Equal(singleDenseVectorData1.NormalizedVectorData, vector1.NormalizedVectorData);
        Assert.Equal(singleDenseVectorData1.Gpt35VectorData, vector1.Gpt35VectorData);
        Assert.Equal(singleDenseVectorData1.Gpt35NormalizedVectorData, vector1.Gpt35NormalizedVectorData);

        var vector2 = reloaded.GetVector(vectorName2);
        Assert.NotNull(vector2);
        Assert.IsType<SingleDenseVectorData>(vector2);
        Assert.Equal(singleDenseVectorData2.VectorData, vector2.VectorData);
        Assert.Equal(singleDenseVectorData2.NormalizedVectorData, vector2.NormalizedVectorData);
        Assert.Equal(singleDenseVectorData2.Gpt35VectorData, vector2.Gpt35VectorData);
        Assert.Equal(singleDenseVectorData2.Gpt35NormalizedVectorData, vector2.Gpt35NormalizedVectorData);
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

    [Fact]
    public async Task Index_text_properties()
    {
        await _sut.InitIndexAsync(_indexName);

        ElasticDocument data = new(Guid.NewGuid().ToString());
        data.Title = "Test Title";
        data.AddTextProperty("prova", "This is a super long test and can be analyzed");

        var insert = await _sut.IndexAsync(_indexName, new ElasticDocument[] { data });
        Assert.True(insert, "Bulk insert did not work");

        ElasticDocument? reloaded = await _sut.GetByIdAsync(_indexName, data.Id);

        Assert.NotNull(reloaded);

        //Assert equality
        Assert.Equal(data.Id, reloaded.Id);

        Assert.Equal(data.Title, reloaded.Title);
        Assert.Equal(data["t_prova"], reloaded["t_prova"]);
    }

    private async Task<(string vectorFieldName, string documentId, SingleDenseVectorData vectorData)> CreateIndexAndIndexDataWithVectors()
    {
        //when the service is inited it should cretate a mapping
        await _sut.InitIndexAsync(_indexName);

        //Arrange, save a document
        ElasticDocument data = await SaveADocument();

        //Act: add a new dense vector.
        var vectorName = "test_vector_" + Guid.NewGuid().ToString().Replace("-", "");

        SingleDenseVectorData singleDenseVectorData = CreateSingleDenseVectorData(data, vectorName);
        SingleDenseVectorData[] vectorList = new SingleDenseVectorData[] { singleDenseVectorData };
        await _sut.IndexDenseVectorAsync(_indexName, vectorList);
        return (vectorName, data.Id, singleDenseVectorData);
    }

    private SingleDenseVectorData CreateSingleDenseVectorData(ElasticDocument data, string vectorName)
    {
        return new SingleDenseVectorData(
                    data.Id,
                    vectorName,
                    VectorData: GenerateRandomVector(512),
                    NormalizedVectorData: GenerateRandomVector(512),
                    Gpt35NormalizedVectorData: GenerateRandomVector(512),
                    Gpt35VectorData: GenerateRandomVector(512));
    }

    private async Task<ElasticDocument> SaveADocument()
    {
        ElasticDocument data = new(Guid.NewGuid().ToString());
        data.Title = "Test Title";
        data["s_metadata1"] = "BLAAAA";
        data["s_metadata2"] = "this is a test";

        var insert = await _sut.IndexAsync(_indexName, new ElasticDocument[] { data });
        Assert.True(insert, "Bulk insert did not work");
        return data;
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
