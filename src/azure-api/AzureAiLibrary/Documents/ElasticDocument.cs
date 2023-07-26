using Nest;
using Serilog;

public class ElasticDocument : Dictionary<string, object>
{
    public string Id { get; set; }

    public ElasticDocument()
    {
    }

    public ElasticDocument(string id)
    {
        Id = id;
    }
}

public class ElasticSearchService
{
    private readonly ElasticClient _elasticClient;

    private ILogger Logger = Log.ForContext<ElasticSearchService>();

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

    public async Task<bool> InitIndexAsync(string indexName)
    {
        //we need to check if the index esitsts, if not create it
        var createIndexResponse = await _elasticClient
             .Indices.CreateAsync(
                 indexName,
                 c => c.Index(indexName)
                     .Settings(s => s
                         .NumberOfReplicas(1)
                         .NumberOfShards(2)
                         .Analysis(CreateIndexSettingsAnalysisDescriptor))
                     .Map(m => m
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

    public async Task<bool> IndexAsync(string indexName, IReadOnlyCollection<ElasticDocument> documents)
    {
        var bulk = new Nest.BulkDescriptor();

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

    public static Func<DynamicTemplateContainerDescriptor<Object>, IPromise<IDynamicTemplateContainer>> MapDynamicProperties
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
                                    //Super-Important: change <see cref="PreMapProperties"/> with the same mapping
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
}