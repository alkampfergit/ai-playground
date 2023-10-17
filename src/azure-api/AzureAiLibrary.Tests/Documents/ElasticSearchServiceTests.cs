using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.DocumentChat;
using Nest;
using static AzureAiLibrary.Documents.ElasticSearchService;

namespace AzureAiLibrary.Tests.Documents;

public class ElasticSearchServiceTests : IDisposable, IAsyncLifetime
{
    private readonly ElasticSearchService _sut;
    private readonly ElasticClient _elasticClient;
    private readonly string _indexName;
    private readonly Random _random;

    public ElasticSearchServiceTests()
    {
        Uri uri = new Uri("http://localhost:9200");
        _sut = new ElasticSearchService(uri);

        _elasticClient = new ElasticClient(uri);

        _indexName = "test_" + Guid.NewGuid().ToString().Replace("-", "");
        _random = new Random((int)DateTime.Now.Ticks);
    }

    public Task InitializeAsync()
    {
        return _sut.InitIndexAsync(_indexName);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _elasticClient.Indices.Delete(_indexName + "*");
    }

    #region Standard Tests

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
    public async Task Basic_store_and_retrieve()
    {
        //when the service is inited it should cretate a mapping
        await _sut.InitIndexAsync(_indexName);
        var doc = new ElasticDocument(Guid.NewGuid().ToString());
        doc.AddStringProperty("customer", "foo");
        doc.AddNumericProperty("bar", 2);

        var insert = await _sut.IndexAsync(_indexName, new[] { doc });
        await _sut.Refresh(_indexName);
        ElasticDocument? reloaded = await _sut.GetByIdAsync(_indexName, doc.Id);
        Assert.NotNull(reloaded);

        //verify properteis
        Assert.Equal("foo", doc.GetStringProperty("customer"));
        Assert.Equal(2, doc.GetNumericProperty("bar"));
    }

    [Fact]
    public async Task Can_delete_by_query()
    {
        //when the service is inited it should cretate a mapping
        await _sut.InitIndexAsync(_indexName);
        var doc1 = new ElasticDocument(Guid.NewGuid().ToString());
        doc1.AddStringProperty("customer", "foo");
        var doc2 = new ElasticDocument(Guid.NewGuid().ToString());
        doc2.AddStringProperty("customer", "baz");
        var insert = await _sut.IndexAsync(_indexName, new[] { doc1, doc2 });
        await _sut.Refresh(_indexName);
        ElasticDocument? reloaded = await _sut.GetByIdAsync(_indexName, doc1.Id);
        Assert.NotNull(reloaded);

        await _sut.DeleteByStringPropertyAsync(_indexName, "customer", "foo");
        await _sut.Refresh(_indexName);

        reloaded = await _sut.GetByIdAsync(_indexName, doc1.Id);
        Assert.Null(reloaded);
        reloaded = await _sut.GetByIdAsync(_indexName, doc2.Id);
        Assert.NotNull(reloaded);
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

        ElasticDocument data = new(Guid.NewGuid().ToString());
        data.Title = "Test Title";
        data.AddStringProperty("ner", new[] { "api", "chatgp", "ai" });

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
    public async Task Can_index_numeric_properites()
    {
        ElasticDocument data = new(Guid.NewGuid().ToString());
        data.Title = "Test Title";
        data.AddNumericProperty("num", 23);

        //index the document.
        Assert.True(await _sut.IndexAsync(_indexName, new ElasticDocument[] { data }));

        //assert: verify searching capabilities
        ElasticDocument? reloaded = await _sut.GetByIdAsync(_indexName, data.Id);

        Assert.NotNull(reloaded);

        //Assert equality
        Assert.Equal(data.Id, reloaded.Id);

        Assert.Equal(data.Title, reloaded.Title);
        Assert.Equal(23, Convert.ToDouble(reloaded["n_num"]));
        Assert.Equal(23, reloaded.GetNumericProperty("num"));
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
    public async Task Can_add_single_vector_data_without_gpt()
    {
        //when the service is inited it should cretate a mapping
        await _sut.InitIndexAsync(_indexName);

        //Arrange, save a document
        ElasticDocument data = await SaveADocument();

        //Act: add a new dense vector.
        var vectorName = "test_vector_" + Guid.NewGuid().ToString().Replace("-", "");

        SingleDenseVectorData singleDenseVectorData = new SingleDenseVectorData(
            data.Id,
            vectorName,
            VectorData: GenerateRandomVector(512),
            NormalizedVectorData: GenerateRandomVector(512),
            Gpt35NormalizedVectorData: null,
            Gpt35VectorData: null);
        SingleDenseVectorData[] vectorList = new SingleDenseVectorData[] { singleDenseVectorData };
        await _sut.IndexDenseVectorAsync(_indexName, vectorList);

        //Assert: retrive the data and verify vectors are there
        ElasticDocument? reloaded = await _sut.GetByIdAsync(_indexName, data.Id);
        Assert.NotNull(reloaded);

        //ok we need now to access the vectors.
        var vector = reloaded.GetVector(vectorName);
        Assert.NotNull(vector);
        Assert.IsType<SingleDenseVectorData>(vector);
        var v = (SingleDenseVectorData)vector;
        Assert.Equal(singleDenseVectorData.VectorData, v.VectorData);
        Assert.Equal(singleDenseVectorData.NormalizedVectorData, v.NormalizedVectorData);
        Assert.Equal(singleDenseVectorData.Gpt35VectorData, v.Gpt35VectorData);
        Assert.Equal(singleDenseVectorData.Gpt35NormalizedVectorData, v.Gpt35NormalizedVectorData);
    }

    [Fact]
    public async Task Can_add_single_vector_data_without_normalized_parts()
    {
        //when the service is inited it should cretate a mapping
        await _sut.InitIndexAsync(_indexName);

        //Arrange, save a document
        ElasticDocument data = await SaveADocument();

        //Act: add a new dense vector.
        var vectorName = "test_vector_" + Guid.NewGuid().ToString().Replace("-", "");

        SingleDenseVectorData singleDenseVectorData = new SingleDenseVectorData(
            data.Id,
            vectorName,
            VectorData: GenerateRandomVector(512),
            NormalizedVectorData: null,
            Gpt35NormalizedVectorData: null,
            Gpt35VectorData: GenerateRandomVector(512));
        SingleDenseVectorData[] vectorList = new SingleDenseVectorData[] { singleDenseVectorData };
        await _sut.IndexDenseVectorAsync(_indexName, vectorList);

        //Assert: retrive the data and verify vectors are there
        ElasticDocument? reloaded = await _sut.GetByIdAsync(_indexName, data.Id);
        Assert.NotNull(reloaded);

        //ok we need now to access the vectors.
        var vector = reloaded.GetVector(vectorName);
        Assert.NotNull(vector);
        Assert.IsType<SingleDenseVectorData>(vector);
        var v = (SingleDenseVectorData)vector;
        Assert.Equal(singleDenseVectorData.VectorData, v.VectorData);
        Assert.Equal(singleDenseVectorData.NormalizedVectorData, v.NormalizedVectorData);
        Assert.Equal(singleDenseVectorData.Gpt35VectorData, v.Gpt35VectorData);
        Assert.Equal(singleDenseVectorData.Gpt35NormalizedVectorData, v.Gpt35NormalizedVectorData);
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
        Assert.IsType<SingleDenseVectorData>(vector2);
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
        SingleDenseVectorData[] vectorList1 = new SingleDenseVectorData[] { singleDenseVectorData1, };
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

    [Fact]
    public async Task Can_search_keyword()
    {
        ElasticDocument data = new(Guid.NewGuid().ToString());
        data.Title = "Test Title";
        data.AddTextProperty("content", "This document contains a unique words complete mediation a technuque used in ... ");

        //index the document.
        Assert.True(await _sut.IndexAsync(_indexName, new ElasticDocument[] { data }));
        _elasticClient.Indices.Refresh(_indexName); //be sure index is refreshed.

        //assert: verify searching capabilities
        var results = await _sut.SearchAsync(_indexName, new[] { "content" }, "complete mediation");
        Assert.Equal(1, results.Count);
        Assert.Equal(data.Id, results.Single().Id);
        Assert.Equal(data.Title, results.Single().Title);

        //check text properties are corrects.
        Assert.Equal(data.GetTextProperty("content"), results.Single().GetTextProperty("content"));
    }

    [Fact]
    public async Task Can_search_multiple_fields_weigth()
    {
        ElasticDocument data1 = new(Guid.NewGuid().ToString());
        data1.Title = "Test Title1";
        data1.AddTextProperty("content", "This is a brief introduction to penetration testing technique ");
        data1.AddTextProperty("gpt35content", "Introduction to pentest");

        ElasticDocument data2 = new(Guid.NewGuid().ToString());
        data2.Title = "Test Title2";
        data2.AddTextProperty("content", "When we get a penetration testing engagement we need to be sure that we have defined ... ");
        data2.AddTextProperty("gpt35content", "Penetration testing rule of engagement");

        //index the document.
        Assert.True(await _sut.IndexAsync(_indexName, new ElasticDocument[] { data1, data2 }));
        _elasticClient.Indices.Refresh(_indexName); //be sure index is refreshed.

        QueryDefinition query = new(_indexName, "penetration testing");
        query.FieldsDefinition.Add(new FieldsDefinition("content", 1));
        query.FieldsDefinition.Add(new FieldsDefinition("gpt35content", 3));

        //assert: verify searching capabilities
        var results = await _sut.SearchAsync(query);
        Assert.Equal(2, results.Count); //both documents matches penetration testing keyword.
        Assert.Equal(data2.Id, results.First().Id); //second element matched penetration testing in both fields.
    }

    [Fact]
    public async Task can_search_vector()
    {
        ElasticDocument data = await IndexDataVectorAndReturnFirstDaeta();

        //assert: verify searching vectgor capabilities.
        var results = await _sut.SearchVectorAsync(
            _indexName,
            "blabla",
            new[] { 1, 0.7, 0.9 });
        Assert.Equal(2, results.Count);
        var first = results.First();
        Assert.True(data.Id == first.Id, "first result is the correct one based on vector distance");
    }

    [Fact]
    public async Task can_search_vector_with_query_model()
    {
        ElasticDocument data = await IndexDataVectorAndReturnFirstDaeta();

        QueryDefinition query = new(_indexName, "penetration testing");
        query.VectorSearches.Add(new VectorSearch("blabla", new[] { 1, 0.7, 0.9 }, UseGptVector: false));

        //assert: verify searching vectgor capabilities.
        var results = await _sut.SearchAsync(query);
        Assert.Equal(2, results.Count);
        var first = results.First();
        Assert.True(data.Id == first.Id, "first result is the correct one based on vector distance");
    }

    [Fact]
    public async Task can_search_vector_gpt_()
    {
        ElasticDocument data = new(Guid.NewGuid().ToString());
        data.Title = "Test Title1";
        data.AddTextProperty("content", "Some content ");

        ElasticDocument data2 = new(Guid.NewGuid().ToString());
        data2.Title = "Test Title2";
        data2.AddTextProperty("content", "Some content ");

        //index the document.
        Assert.True(await _sut.IndexAsync(_indexName, new ElasticDocument[] { data, data2 }));

        //now add vectors
        SingleDenseVectorData singleDenseVectorData = new SingleDenseVectorData(
            data.Id,
            "blabla",
            VectorData: new double[] { 1, 1, 1 },
            NormalizedVectorData: new double[] { 0.5774, 0.5774, 0.5774 },
            Gpt35VectorData: new double[] { 1, 1, 1 },
            Gpt35NormalizedVectorData: new double[] { 0.5774, 0.5774, 0.5774 });

        //Second vector, actually a unit length vector.
        SingleDenseVectorData singleDenseVectorData2 = new SingleDenseVectorData(
            data2.Id,
            "blabla",
            VectorData: new double[] { 0, 1, 0 },
            NormalizedVectorData: new double[] { 0, 1, 0 },
            Gpt35VectorData: new double[] { 0, 1, 0 },
            Gpt35NormalizedVectorData: new double[] { 0, 1, 0 });

        await _sut.IndexDenseVectorAsync(_indexName, new[] { singleDenseVectorData, singleDenseVectorData2 });
        await _elasticClient.Indices.RefreshAsync(_indexName); //be sure index is refreshed.

        //assert: verify searching vectgor capabilities.
        var results = await _sut.SearchVectorAsync(
            _indexName,
            "blabla",
            new[] { 1, 0.7, 0.9 });
        Assert.Equal(2, results.Count);
        var first = results.First();
        Assert.True(data.Id == first.Id, "first result is the correct one based on vector distance");
    }

    private async Task<ElasticDocument> IndexDataVectorAndReturnFirstDaeta()
    {
        ElasticDocument data = new(Guid.NewGuid().ToString());
        data.Title = "Test Title1";
        data.AddTextProperty("content", "Some content ");

        ElasticDocument data2 = new(Guid.NewGuid().ToString());
        data2.Title = "Test Title2";
        data2.AddTextProperty("content", "Some content ");

        //index the document.
        Assert.True(await _sut.IndexAsync(_indexName, new ElasticDocument[] { data, data2 }));

        //now add vectors
        SingleDenseVectorData singleDenseVectorData = new SingleDenseVectorData(
            data.Id,
            "blabla",
            VectorData: new double[] { 1, 1, 1 },
            NormalizedVectorData: new double[] { 0.5774, 0.5774, 0.5774 },
            Gpt35VectorData: new double[] { 1, 1, 1 },
            Gpt35NormalizedVectorData: new double[] { 0.5774, 0.5774, 0.5774 });

        //Second vector, actually a unit length vector.
        SingleDenseVectorData singleDenseVectorData2 = new SingleDenseVectorData(
            data2.Id,
            "blabla",
            VectorData: new double[] { 0, 1, 0 },
            NormalizedVectorData: new double[] { 0, 1, 0 },
            Gpt35VectorData: new double[] { 0, 1, 0 },
            Gpt35NormalizedVectorData: new double[] { 0, 1, 0 });

        await _sut.IndexDenseVectorAsync(_indexName, new[] { singleDenseVectorData, singleDenseVectorData2 });
        await _elasticClient.Indices.RefreshAsync(_indexName); //be sure index is refreshed.
        return data;
    }

    [Fact]
    public async Task can_search_vector_when_not_all_documents_has_vector()
    {
        ElasticDocument data = new(Guid.NewGuid().ToString());
        data.Title = "Test Title1";
        data.AddTextProperty("content", "Some content ");

        ElasticDocument data2 = new(Guid.NewGuid().ToString());
        data2.Title = "Test Title2";
        data2.AddTextProperty("content", "Some content ");

        //index the document.
        Assert.True(await _sut.IndexAsync(_indexName, new ElasticDocument[] { data, data2 }));

        //now add vectors
        SingleDenseVectorData singleDenseVectorData = new SingleDenseVectorData(
            data.Id,
            "blabla",
            VectorData: new double[] { 1, 1, 1 },
            NormalizedVectorData: new double[] { 0.5774, 0.5774, 0.5774 },
            Gpt35VectorData: new double[] { 1, 1, 1 },
            Gpt35NormalizedVectorData: new double[] { 0.5774, 0.5774, 0.5774 });

        //only the first document has vector data blabla 
        await _sut.IndexDenseVectorAsync(_indexName, new[] { singleDenseVectorData });
        await _elasticClient.Indices.RefreshAsync(_indexName); //be sure index is refreshed.

        //assert: verify searching vectgor capabilities.
        var results = await _sut.SearchVectorAsync(
            _indexName,
            "blabla",
            new[] { 1, 0.7, 0.9 });
        Assert.Equal(1, results.Count);
        var first = results.Single();
        Assert.True(data.Id == first.Id, "first result is the correct one based on vector distance");
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
    #endregion

    #region Specific subclasses Tests

    /// <summary>
    /// Properties can be stored as array of strings, there is no problem because elastic
    /// does not distinguish between single value and array of values.
    /// </summary>
    [Fact]
    public async Task Index_segmented_document()
    {
        await _sut.InitIndexAsync(_indexName);

        ElasticDocumentSegment data = new("foo12", "This is a content", 3);

        var insert = await _sut.IndexAsync(_indexName, new ElasticDocumentSegment[] { data });

        Assert.True(insert, "Bulk insert did not work");

        ///Get the new element.
        ElasticDocumentSegment? reloaded = await _sut.GetByIdAsync<ElasticDocumentSegment>(_indexName, data.Id);

        Assert.NotNull(reloaded);

        //Assert equality
        Assert.Equal(data.Id, reloaded.Id);

        Assert.Equal(data.Title, reloaded.Title);
        Assert.Equal(data.Content, reloaded.Content);
        Assert.Equal(data.PageId, reloaded.PageId);
    }

    #endregion
}
