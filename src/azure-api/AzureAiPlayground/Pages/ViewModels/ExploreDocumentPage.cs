using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AzureAiPlayground.Pages.ViewModels;

public class ExploreDocumentPage : ComponentBase
{
    public string? FilePathToImport { get; set; }

    public UiSingleDocument? CurrentDocument { get; set; }
    
    [Inject] public ExploreDocumentViewModel DocumentsViewModel { get; set; } = null!;

    public async Task ExtractDocument()
    {
        if (String.IsNullOrEmpty(FilePathToImport)) return;

        var id = System.IO.Path.GetFileName(FilePathToImport);
        CurrentDocument = await DocumentsViewModel.ExtractDocument(FilePathToImport!, id);
    }
}