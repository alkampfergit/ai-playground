using Nest;
using Serilog;
using System.Text;

namespace AzureAiLibrary.Documents;

public class ElasticSearchService
{
    private readonly ElasticClient _elasticClient;
    private readonly Uri _uri;
    private ILogger Logger = Log.ForContext<ElasticSearchService>();

    public ElasticSearchService(Uri uri)
    {
        var settings = new ConnectionSettings(uri);
        _elasticClient = new ElasticClient(settings);
        _uri = uri;
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

    public async Task<bool> InitIndexAsync(string indexName)
    {
        //we need to check if the index esitsts, if not create 
        var indexExists = await _elasticClient.Indices.ExistsAsync(indexName);

        if (indexExists.Exists) return true;

        var createIndexResponse = await _elasticClient
             .Indices.CreateAsync(
                 indexName,
                 c => c.Index(indexName)
                     .Settings(s => s
                         .NumberOfReplicas(1)
                         .NumberOfShards(2)
                         .Analysis(CreateIndexSettingsAnalysisDescriptor))
                     .Map<ElasticDocument>(m => m
                         .AutoMap()
                         .Properties(props => props
                            //.DenseVector(dv => dv.Name("bert").Dimensions(512)) //todo this should be added directly from adding vectors.
                            .Text(dv => dv.Name(d => d.Title).Analyzer("english"))
                         )
                         //.RoutingField(r => r.Required())
                         //.AutoMap<OmniSearchItemSecurityDescriptor>()
                         //.AutoMap<OmniSearchItem>()
                         //.SourceField(source => source.Enabled(true))
                         //.Properties(OmniSearchIndexMapper7.MapSecurityDescriptor)
                         //.Properties(OmniSearchIndexMapper7.MapProperties7)
                         //.Properties<OmniSearchItem>(props => props
                         //        .Join(j => j
                         //            .Name(osi => osi.JoinField)
                         //            .Relations(r => r.Join<OmniSearchItemSecurityDescriptor, OmniSearchItem>()))
                         //        )
                         .DynamicTemplates(MapDynamicProperties)
                     )
         );

        if (!createIndexResponse.IsValid)
        {
            Logger.Error("Error creating index {indexName}: {error}", indexName, createIndexResponse.DebugInformation);
        }

        return createIndexResponse.IsValid;
    }

    public async Task<bool> IndexAsync(string indexName, IReadOnlyCollection<ElasticDocument> documents)
    {
        var bulk = new BulkDescriptor();

        foreach (dynamic document in documents)
        {
            bulk.Index<ElasticDocument>(op =>
            {
                op.Document(document)
                    .Id(document.Id) //remember to use the id when index <Object>
                    .Index(indexName);

                return op;
            });
        }

        var result = await _elasticClient.BulkAsync(bulk);
        if (!result.IsValid)
        {
            Logger.Error($"Unable to bulk insert nodes: {result.ItemsWithErrors.Count()} items were not saved correctly on a total of {documents.Count}. {result.ServerError} {result.DebugInformation}", result.OriginalException);

            //todo: better error message, better error handling
        }

        return result.IsValid;
    }

    public async Task<ElasticDocument?> GetByIdAsync(string indexName, string id)
    {
        var request = new GetRequest(indexName, new Id(id));
        var response = await _elasticClient.GetAsync<ElasticDocument>(request);
        if (!response.IsValid)
        {
            Logger.Error($"Unable to get element with id {id} error {response.DebugInformation}");
            return null;
        }
        response.Source.Id = id;
        return response.Source;
    }

    public const string StringPropertiesAnalyzer = "string_props";
    public const string AnalyzerNgramStandard = "edge_ngram_standard_analyzer";
    public const string AnalyzerTrigramStandard = "trigram_standard";
    public const string AnalyzerPropertiesIndexTime = "property_indexTime";
    public const string NotAnalyzedLowercase = "not_analyzed_lowercase";
    public const string NonAsciiAndSpaceSplittedLowerCaseAnalyzerName = "non_ascii_and_space_split_lowercase";

    public const string RemoveZeroCharFilter = "trim_zero_chars";
    public const string NonAsciiAndSpaceSplittedLowerCaseTokenizer = "non_ascii_and_space_split_lowercase_tokenizer";
    public const string MainSearchAnalyzer = "mainsearch";

    /// <summary>
    /// Creating mapping descriptor for new version of the driver
    /// </summary>
    private static AnalysisDescriptor CreateIndexSettingsAnalysisDescriptor(AnalysisDescriptor descriptor)
    {
        PathHierarchyTokenizer pathtokenizer = new PathHierarchyTokenizer()
        {
            Delimiter = '\\',
        };

        KeywordTokenizer keywordTokenizer = new KeywordTokenizer();
        LowercaseTokenFilter lowerCaseTokenFilter = new LowercaseTokenFilter();

        descriptor.Tokenizers(t => t
            .PathHierarchy("path_tokenizer", _ => pathtokenizer)
            .Keyword("keyword_tokenizer", _ => keywordTokenizer)
            .EdgeNGram("edge_ngram_tokenizer", d => d
                .MinGram(3)
                .MaxGram(10)
            )
            .NGram("trigram_tokenizer", d => d
                .MinGram(3)
                .MaxGram(3)
            )
            .Pattern(NonAsciiAndSpaceSplittedLowerCaseTokenizer, pt => pt
                .Pattern("(?<=[^\\p{ASCII}]|\\s)") //split for NON ASCII char or space and keep the splitting group.
                .Group(-1) //split mode
                .Flags("CASE_INSENSITIVE|MULTILINE"))
        ).TokenFilters(tf => tf
            .Lowercase("lowercase_filter", _ => lowerCaseTokenFilter)
            .EdgeNGram("edge_ngram_filter_standard", d => d
                .MinGram(2)
                .MaxGram(15)
            )
            .Length(RemoveZeroCharFilter, td => td.Min(1).Max(100))
        ).Analyzers(an => an
            .Custom("path", d => new CustomAnalyzer() { Tokenizer = "path_tokenizer" })
            .Custom(NotAnalyzedLowercase, _ => new CustomAnalyzer()
            {
                Tokenizer = "keyword_tokenizer",
                Filter = new[] { "lowercase_filter", "asciifolding" },
            })
            .Custom(MainSearchAnalyzer, d => new CustomAnalyzer() // if you change this, change accordingly the AnalyzerNgramStandard
            {
                Tokenizer = "standard",
                Filter = new[]
                {
                            "lowercase",
                            "asciifolding"
                }
            })
            .Custom(StringPropertiesAnalyzer, d => new CustomAnalyzer()
            {
                Tokenizer = "standard",
                Filter = new[]
                {
                            "lowercase",
                            "asciifolding"
                }
            })
            .Custom(AnalyzerNgramStandard, _ => new CustomAnalyzer() //this must reflect the very same configuratino of MainSearchAnalyzer without edge ngram
            {
                Tokenizer = "standard", //standard tokenizer is ok.
                Filter = new[]
                {
                            "lowercase_filter",
                            "asciifolding",
                            "edge_ngram_filter_standard" //edge ngram to create prefix query in a more efficient way.
                }
            })
            .Custom(AnalyzerTrigramStandard, _ => new CustomAnalyzer()
            {
                Tokenizer = "trigram_tokenizer",
                Filter = new[]
                {
                            "lowercase_filter"
                }
            })
            .Custom(AnalyzerPropertiesIndexTime, d => new CustomAnalyzer()
            {
                Tokenizer = "standard",
                Filter = new[]
                {
                            "lowercase"
                }
            })
            .Custom(NonAsciiAndSpaceSplittedLowerCaseAnalyzerName, d => new CustomAnalyzer()
            {
                Tokenizer = NonAsciiAndSpaceSplittedLowerCaseTokenizer,
                Filter = new[]
                {
                    "trim",
                    RemoveZeroCharFilter
                }
            })
        );

        return descriptor;
    }

    private HashSet<string> _alreadyMappedDenseVectorField = new();

    private readonly Func<string, string> standardVectorProperty = fieldName => $"v_{fieldName}_vector";
    private readonly Func<string, string> standardNormalizedVectorProperty = fieldName => $"v_{fieldName}_normalized_vector";
    private readonly Func<string, string> gpt35VectorProperty = fieldName => $"v_{fieldName}_gpt35_vector";
    private readonly Func<string, string> gpt35NormalizedVectorProperty = fieldName => $"v_{fieldName}_gpt35_normalized_vector";

    public async Task IndexDenseVectorAsync(string indexName, SingleDenseVectorData[] singleDenseVectorDatas)
    {
        if (singleDenseVectorDatas?.Length > 0)
        {
            var allVectorDatas = singleDenseVectorDatas
                .GroupBy(d => d.FieldName)
                .Select(d => new
                {
                    FieldName = d.ElementAt(0).FieldName,
                    Length = d.ElementAt(0).VectorData.Length
                }).ToArray();

            //Map everything that is in the call.
            foreach (var fieldInformation in allVectorDatas)
            {
                var fieldName = fieldInformation.FieldName;
                var dimension = fieldInformation.Length;

                //need to know if the vector was already mapped
                var indexKey = $"{indexName}_{fieldName}";
                if (!_alreadyMappedDenseVectorField.Contains(indexKey))
                {
                    await EnsureMappingAsync(indexName, fieldName, dimension);
                    _alreadyMappedDenseVectorField.Add(indexKey);
                }
            }

            //ok the dense vector is mapped, we need to create a bulk request
            var bulk = new BulkDescriptor();

            foreach (var vectorData in singleDenseVectorDatas.GroupBy(d => d.Id))
            {
                //ok we can have more than one dense vector for each id.
                bulk.Update<ElasticDocument>(u => u
                     .Id(vectorData.Key)
                     .Index(indexName)
                     .Script(s => s
                         .Source(CreateSourceUpdate(vectorData))
                         .Params(AddParams(vectorData))
                     )
                 );
            }

            var bulkUpdateResult = await _elasticClient.BulkAsync(bulk);
            if (bulkUpdateResult.IsValid == false)
            {
                throw new Exception("Unable to update dense vector for index " + indexName + " " + bulkUpdateResult.DebugInformation);
            }
        }
    }

    private Dictionary<string, object> AddParams(IGrouping<string, SingleDenseVectorData> vectorData)
    {
        var ret = new Dictionary<string, object>();
        foreach (var vector in vectorData)
        {
            if (vector.Gpt35VectorData != null)
            {
                ret.Add($"standardVectorProperty{vector.FieldName}", vector.VectorData); // new value for foo as a dense vector
                ret.Add($"standardNormalizedVectorProperty{vector.FieldName}", vector.NormalizedVectorData);
                ret.Add($"gpt35VectorProperty{vector.FieldName}", vector.Gpt35VectorData ?? Array.Empty<double>());
                ret.Add($"gpt35NormalizedVectorProperty{vector.FieldName}", vector.Gpt35NormalizedVectorData ?? Array.Empty<double>());
            }
            else
            {
                ret.Add($"standardVectorProperty{vector.FieldName}", vector.VectorData); // new value for foo as a dense vector
                ret.Add($"standardNormalizedVectorProperty{vector.FieldName}", vector.NormalizedVectorData);
            }
        }

        return ret;
    }

    private string CreateSourceUpdate(IGrouping<string, SingleDenseVectorData> vectorData)
    {
        StringBuilder sb = new StringBuilder(500);
        foreach (var vector in vectorData)
        {
            var fieldName = vector.FieldName;
            if (vector.Gpt35VectorData != null)
            {
                sb.Append($@"ctx._source.{standardVectorProperty(fieldName)} = params.standardVectorProperty{fieldName}; 
ctx._source.{standardNormalizedVectorProperty(fieldName)} = params.standardNormalizedVectorProperty{fieldName}; 
ctx._source.{gpt35VectorProperty(fieldName)} = params.gpt35VectorProperty{fieldName}; 
ctx._source.{gpt35NormalizedVectorProperty(fieldName)} = params.gpt35NormalizedVectorProperty{fieldName};
");
            }
            else
            {
                sb.Append($@"ctx._source.{standardVectorProperty(fieldName)} = params.standardVectorProperty{fieldName}; 
ctx._source.{standardNormalizedVectorProperty(fieldName)} = params.standardNormalizedVectorProperty{fieldName}; 
");
            }
        }

        return sb.ToString();
    }

    private async Task EnsureMappingAsync(
        string indexName,
        string fieldName,
        int dimension)
    {
        var request = new GetMappingRequest(indexName);

        var mappings = await _elasticClient.Indices.GetMappingAsync(request);
        if (mappings.IsValid == false)
        {
            throw new Exception("Unable to get mapping for index " + indexName);
        }
        var mapping = mappings.GetMappingFor(indexName);
        if (mapping.Properties.ContainsKey(fieldName))
        {
            //TODO: Check for dimensions
            return;
        }

        //ok we need to map the field
        var mapResult = await _elasticClient.MapAsync<ElasticDocument>(
            r => r.Index(indexName)
                .Properties(p => p
                    .DenseVector(d => d.Name(standardVectorProperty(fieldName)).Dimensions(dimension))
                    .DenseVector(d => d.Name(standardNormalizedVectorProperty(fieldName)).Dimensions(dimension))
                    .DenseVector(d => d.Name(gpt35VectorProperty(fieldName)).Dimensions(dimension))
                    .DenseVector(d => d.Name(gpt35NormalizedVectorProperty(fieldName)).Dimensions(dimension)
        )));

        if (mapResult.IsValid == false)
        {
            throw new Exception("Unable to SET mapping for index " + indexName);
        }
    }

    /// <summary>
    /// Performs a keyword xact match search on the specified fields.
    /// </summary>
    /// <param name="indexName"></param>
    /// <param name="fields"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<IReadOnlyCollection<ElasticDocument>> SearchAsync(string indexName, string[] fields, string query)
    {
        //perform an ElasticSearch query with query string frield with the nest driver
        var searchResult = await _elasticClient.SearchAsync<ElasticDocument>(s => s
            .Index(indexName)
                .Size(20)
                .Query(q => q
                    .QueryString(qs => qs
                    .Query(query)
                        .Fields(fields.Select(f => $"t_{f}").ToArray())
                    )
            ));

        return PostQuery(searchResult);
    }

    public async Task<IReadOnlyCollection<ElasticDocument>> SearchVectorAsync(
        string indexName,
        string vectorType,
        double[] similarityVector,
        bool useGptVector = false)
    {
        //I need to perform a NEST query on vector field identified by vectorField. In this first version
        //we let the caller to specify if we want to use the gpt35 vector or the standard vector.
        var vectorFieldName = useGptVector ? gpt35VectorProperty(vectorType) : standardVectorProperty(vectorType);
        var searchResponse = await _elasticClient.SearchAsync<ElasticDocument>(s => s
            .Index(indexName)
            .Query(q => q
                .ScriptScore(ss => ss
                    .Query(qq => qq.MatchAll())
                    .Script(sc => sc
                        .Source($"cosineSimilarity(params.queryVector, doc['{vectorFieldName}']) + 1.0")
                        .Params(p => p
                            .Add("queryVector", similarityVector)
                        )
                    )
                )
            )
        );

        return PostQuery(searchResponse);
    }

    private IReadOnlyCollection<ElasticDocument> PostQuery(ISearchResponse<ElasticDocument> searchResult)
    {
        if (!searchResult.IsValid)
        {
            Logger.Error("Error searching inside elasticsearch: {error}", searchResult.ServerError?.Error);
            return Array.Empty<ElasticDocument>();
        }

        for (int i = 0; i < searchResult.Documents.Count; i++)
        {
            var doc = searchResult.Documents.ElementAt(i);
            var hit = searchResult.Hits.ElementAt(i);
            doc.Id = hit.Id;
        }

        return searchResult.Documents;
    }

    public static Func<DynamicTemplateContainerDescriptor<ElasticDocument>, IPromise<IDynamicTemplateContainer>> MapDynamicProperties
    {
        get
        {
            return t => t
                .DynamicTemplate(
                    "StringProperties", t1 => t1
                        .PathMatch("s_*")
                        .Mapping(m1 => m1
                            .Text(sm => sm
                                .Analyzer(StringPropertiesAnalyzer)
                                .Fields(fp => fp
                                    .Text(fps => fps
                                        .Name(_ => _.Suffix("nal"))
                                        .Analyzer(NotAnalyzedLowercase)
                                    )
                                    .Keyword(fps => fps
                                        .Name(_ => _.Suffix("kw"))
                                    ).Text(fps => fps
                                        .Name(_ => _.Suffix("ws"))
                                        .Analyzer(NonAsciiAndSpaceSplittedLowerCaseAnalyzerName)
                                    )
                                )
                            )
                       )
                )
                .DynamicTemplate(
                    "TextProperties", t1 => t1
                        .PathMatch("t_*")
                        .Mapping(m1 => m1
                            .Text(sm => sm
                                .Analyzer(StringPropertiesAnalyzer)
                                .Fields(fp => fp
                                    .Text(fps => fps
                                        .Name(_ => _.Suffix("ws"))
                                        .Analyzer(NonAsciiAndSpaceSplittedLowerCaseAnalyzerName)
                                    )
                                )
                            )
                       )
                )
                .DynamicTemplate(
                    "NumericProperties", t1 => t1
                        .PathMatch("n_*")
                        .Mapping(m1 => m1
                            .Number(nm => nm.Type(NumberType.Double))
                       )
                )
                .DynamicTemplate(
                    "DateProperties", t1 => t1
                        .PathMatch("d_*")
                        .Mapping(m1 => m1
                            .Date(dm => dm)
                       )
                );
        }


    }

    /// <summary>
    /// Performs a keyword xact match search on the specified fields.
    /// </summary>
    /// <param name="indexName"></param>
    /// <param name="fields"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<IReadOnlyCollection<ElasticDocument>> SearchAsync(QueryDefinition queryDefinition)
    {
        //perform an ElasticSearch query with query string frield with the nest driver
        var searchResult = await _elasticClient.SearchAsync<ElasticDocument>(s => s
            .Index(queryDefinition.Index)
                .Size(queryDefinition.NumOfRecords)
                .Query(q => q
                    .QueryString(qs => qs
                    .Query(queryDefinition.Query)
                        .Fields(queryDefinition.FieldsDefinition.Select(f => $"t_{f.FieldName}^{f.Boost}").ToArray())
                    )
            ));

        return PostQuery(searchResult);
    }

    public class QueryDefinition
    {
        public QueryDefinition(string index, string query)
        {
            Index = index;
            Query = query;
        }

        public string Index { get; private set; }
        public string Query { get; private set; }

        public int NumOfRecords { get; set; } = 20;

        public List<FieldsDefinition> FieldsDefinition { get; set; } = new List<FieldsDefinition>();
    }

    public record FieldsDefinition(string FieldName, double Boost= 1);
}

