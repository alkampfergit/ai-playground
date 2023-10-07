using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AzureAiPlayground.Pages.ViewModels;

public class ExploreDocumentPage : ComponentBase
{
    public string? FilePathToImport { get; set; } = "/Users/gianmariaricci/Downloads/advancedapisecurity.pdf";

    [Inject] public ExploreDocumentViewModel ViewModel { get; set; } = null!;
    public MudTable<UiSingleDocumentPage> ResultTable { get; set; } = null!;
    public MudTable<UiSingleDocumentSegment> SegmentsTable { get; set; } = null!;
    public MudTable<ExploreDocumentSearchViewModel.DocumentContentSearchResult> SearchResultTable { get; set; } = null!;

    public UiSingleDocumentPage? SelectedPage { get; set; }
    public DebugViewModelLog? SelectedLog { get; set; }

    public ExploreDocumentSearchViewModel.DocumentContentSearchResult? SelectedSearchResult { get; set; }

    public async Task ExtractDocument()
    {
        if (String.IsNullOrEmpty(FilePathToImport)) return;

        var id = System.IO.Path.GetFileName(FilePathToImport);
        await ViewModel.ExtractDocument(FilePathToImport!, id);
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await ViewModel.Init();
    }

    public string SelectedRowClassFunc(UiSingleDocumentPage item, int _)
    {
        if (item == SelectedPage)
        {
            return "selected";
        }

        return string.Empty;
    }

    public string SearchResultSelectedRowClassFunc(ExploreDocumentSearchViewModel.DocumentContentSearchResult item, int _)
    {
        if (item == SelectedSearchResult)
        {
            return "selected";
        }

        return string.Empty;
    }

    public string LogSelectedRowClassFunc(DebugViewModelLog item, int _)
    {
        if (item == SelectedLog)
        {
            return "selected";
        }

        return string.Empty;
    }

    public void RowClickEvent(TableRowClickEventArgs<UiSingleDocumentPage> evt)
    {
        SelectedPage = evt.Item;
    }

    public void LogRowClickEvent(TableRowClickEventArgs<DebugViewModelLog> evt)
    {
        if (SelectedLog != null)
        {
            SelectedLog.ShowDetail = false;
        }
        if (SelectedLog == evt.Item)
        {
            //we are clicking on the actual selected row
            SelectedLog = null;
            return;
        }
        SelectedLog = evt.Item;
        SelectedLog.ShowDetail = true;
    }

    public void SearchResultRowClickEvent(TableRowClickEventArgs<ExploreDocumentSearchViewModel.DocumentContentSearchResult> evt)
    {
        if (SelectedSearchResult != null)
        {
            SelectedSearchResult.ShowDetail = false;
        }
        if (SelectedSearchResult == evt.Item)
        {
            //we are clicking on the actual selected row
            SelectedSearchResult = null;
            return;
        }
        SelectedSearchResult = evt.Item;
        SelectedSearchResult.ShowDetail = true;
    }

    public void ShowBtnPress(UiSingleDocumentPage page)
    {
        page.ShowDetails = !page.ShowDetails;
    }
}