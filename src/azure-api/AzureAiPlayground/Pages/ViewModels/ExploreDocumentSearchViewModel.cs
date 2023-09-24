using System.Text;

namespace AzureAiPlayground.Pages.ViewModels;

/// <summary>
/// This class will contains all information about search into ES and display results.
/// </summary>
public class ExploreDocumentSearchViewModel
{
    /// <summary>
    /// When I search into elasticsearch these are the results of the query, it is the
    /// final result of the query that contains all segments that are returnedd from the
    /// query
    /// </summary>
    public IReadOnlyCollection<UiSingleDocumentSegment> SegmentsQueryResults { get; set; } =
        Array.Empty<UiSingleDocumentSegment>();

    /// <summary>
    /// A string builder that contains log of whatever is executed for the search, each
    /// step can add log to the list of logs.
    /// </summary>
    public List<string> Logs { get; set; } = new ();

    public void Clear()
    {
        Logs.Clear();
        SegmentsQueryResults = Array.Empty<UiSingleDocumentSegment>();
    }
}