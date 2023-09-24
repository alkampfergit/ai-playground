using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AzureAiPlayground.Pages.ViewModels;

public class ExploreDocumentPage : ComponentBase
{
    public string? FilePathToImport { get; set; } = "/Users/gianmariaricci/Downloads/advancedapisecurity.pdf";
    
    [Inject] public ExploreDocumentViewModel ViewModel { get; set; } = null!;

    public MudTable<UiSingleDocumentPage> ResultTable { get; set; } = null!;
    public MudTable<UiSingleDocumentSegment> SegmentsTable { get; set; } = null!;

    public UiSingleDocumentPage SelectedPage { get; set; }

    public async Task ExtractDocument()
    {
        if (String.IsNullOrEmpty(FilePathToImport)) return;

        var id = System.IO.Path.GetFileName(FilePathToImport);
        await ViewModel.ExtractDocument(FilePathToImport!, id);
    }
    
    public string SelectedRowClassFunc(UiSingleDocumentPage item, int rowNumber)
    {
        if (item == SelectedPage)
        {
            return "selected";
        }

        return string.Empty;
    }

    public void RowClickEvent(TableRowClickEventArgs<UiSingleDocumentPage> evt)
    {
        SelectedPage = evt.Item;
    }
    
    public void ShowBtnPress(UiSingleDocumentPage page)
    {
        page.ShowDetails = !page.ShowDetails;
    }
}