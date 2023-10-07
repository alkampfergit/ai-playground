using MongoDB.Bson;
using Nest;

namespace AzureAiPlayground.Pages.ViewModels;

/// <summary>
/// This class will contains all information about search into ES/Atlas and display results.
/// </summary>
public class ExploreDocumentSearchViewModel
{
    /// <summary>
    /// When I search into elasticsearch these are the results of the query, it is the
    /// final result of the query that contains all segments that are returned from the
    /// query
    /// </summary>
    public IReadOnlyCollection<DocumentContentSearchResult> SegmentsQueryResults { get; set; } =
        Array.Empty<DocumentContentSearchResult>();

    /// <summary>
    /// A string builder that contains log of whatever is executed for the search, each
    /// step can add log to the list of logs.
    /// </summary>
    public List<string> Logs { get; set; } = new ();
    
    public void Clear()
    {
        Logs.Clear();
        SegmentsQueryResults = Array.Empty<DocumentContentSearchResult>();
    }

    public record Highlight(string Path, string Text)
    {
        private Highlight(BsonDocument be) : this(null!, null!)
        {
            //Construct the element from a projection in atlas search
            Path = be["path"].AsString;
            Text = be["texts"]
                .AsBsonArray
                .Select(e => e["type"].AsString == "hit" ?
                    $"<b>{e["value"].AsString}</b>" :
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
        IReadOnlyCollection<Highlight> Highlights);
}