using MongoDB.Bson;
using MongoDB.Driver;

namespace AzureAiPlayground.Pages.ViewModels;

/// <summary>
/// This class will contains all information about search into ES/Atlas and display results.
/// </summary>
public class ExploreDocumentSearchViewModel
{
    private readonly ExploreDocumentViewModel _owner;
    private readonly IMongoCollection<DocumentSegment> _segmentsCollection;

    /// <summary>
    /// When I search into elasticsearch these are the results of the query, it is the
    /// final result of the query that contains all segments that are returned from the
    /// query
    /// </summary>
    public IReadOnlyCollection<DocumentContentSearchResult> SegmentsQueryResults { get; set; } =
        Array.Empty<DocumentContentSearchResult>();

    public ExploreDocumentSearchViewModel(
        ExploreDocumentViewModel owner,
        IMongoCollection<DocumentSegment> segmentsCollection)
    {
        _owner=owner;
        _segmentsCollection=segmentsCollection;
    }

    public void Clear()
    {
        SegmentsQueryResults = Array.Empty<DocumentContentSearchResult>();
    }

    internal Task SearchKeyword(string keywordSearch, string? documentId)
    {
        return SearchInAtlas(new[] { new BoostedQuery(keywordSearch, 1)}, documentId);
    }

    internal Task SearchKeyword(IReadOnlyCollection<BoostedQuery> queries, string? documentId)
    {
        return SearchInAtlas(queries, documentId);
    }

    private async Task SearchInAtlas(IReadOnlyCollection<BoostedQuery> queries, string? documentId)
    {
        var innerSearchDocuments = queries
            .Select(q => new BsonDocument
                {
                    {
                        "text", new BsonDocument
                        {
                            { "query", q.Query },
                            { "path", "Content" },
                            { "score" , new BsonDocument
                                {
                                    { "boost", new BsonDocument
                                        {
                                            { "value", q.Boost }
                                        }
                                    }
                                }
                            }
                        }
                    }
                })
            .ToArray();
        var searchStage = new BsonDocument
            {
                {
                    "$search", new BsonDocument
                    {
                        { "index", "segments" },
                        {
                            "compound", new BsonDocument
                            {
                                {
                                    "should", new BsonArray(innerSearchDocuments)
                                }
                            }
                        },
                        {
                            "highlight", new BsonDocument
                            {
                                { "path", "Content" }
                            }
                        },
                        {
                            "scoreDetails" , true
                        }
                    }
                }
            };

        var projectStage = new BsonDocument
        {
            {
              "$project", new BsonDocument {
                { "description", 1 },
                { "_id", 0 },
                { "PageNumber", 1 },
                { "Content", 1 },
                { "SingleDocumentId", 1 },
                { "Highlights", new BsonDocument("$meta", "searchHighlights") } }
              }
        };

        var limitStage = new BsonDocument
        {
            {
                "$limit", 10
            }
        };

        _owner.Logs.AddLog( "Atlas search", searchStage);

        var pipeline = new List<BsonDocument> { searchStage, projectStage, limitStage };
        var resultAggregateAsync = await _segmentsCollection.AggregateAsync<BsonDocument>(pipeline, new AggregateOptions());

        var result = await resultAggregateAsync.ToListAsync();

        _owner.Logs.AddLog($"Atlas search returned {result.Count} elements", result);

        SegmentsQueryResults = result
            .Select(d => new ExploreDocumentSearchViewModel.DocumentContentSearchResult(
                d["SingleDocumentId"].AsString,
                d["PageNumber"].AsInt32,
                d["Content"].AsString,
                ExploreDocumentSearchViewModel.Highlight.FromBsonArray((BsonArray)d["Highlights"])
            ))
            .ToList();
    }

    //private async Task SearchInElasticsearch()
    //{
    //    //simple elastic search query
    //    var result = await _esService.SearchAsync(
    //        _segmentsIndexName,
    //        new[] { "content" },
    //        KeywordSearch);

    //    SearchViewModel.SegmentsQueryResults = result
    //        .Select(d => new ExploreDocumentSearchViewModel.DocumentContentSearchResult(
    //            d.GetStringProperty("docid") ?? "",
    //              (int)(d.GetNumericProperty("page") ?? 0),
    //            d.GetTextProperty("content") ?? "",
    //            Array.Empty<ExploreDocumentSearchViewModel.Highlight>()
    //            ))
    //       .ToList();
    //}

    public record Highlight(string Path, string Text)
    {
        private Highlight(BsonDocument be) : this(null!, null!)
        {
            //Construct the element from a projection in atlas search
            Path = be["path"].AsString;
            Text = be["texts"]
                .AsBsonArray
                .Select(e => e["type"].AsString == "hit" ?
                    $"<span style=\"color: red; font-size: 18px; font-weight: bold;\">{e["value"].AsString}</span>" :
                    e["value"].AsString)
                .Aggregate((s1, s2) => s1 + "" + s2);
        }

        public static IReadOnlyCollection<Highlight> FromBsonArray(object bsonArray)
        {
            if (!(bsonArray is BsonArray array))
            {
                return Array.Empty<Highlight>();
            }

            return array
                .Cast<BsonDocument>()
                .Select(e => new Highlight(e))
                .ToArray();
        }
    }

    public record DocumentContentSearchResult(
        string Id,
        int Page,
        string Content,
        IReadOnlyCollection<Highlight> Highlights)
    {
        public Boolean ShowDetail { get; set; } = false;
    }

    public record BoostedQuery(string Query, double Boost);
}