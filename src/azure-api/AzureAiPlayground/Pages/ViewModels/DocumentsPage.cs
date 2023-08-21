using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AzureAiPlayground.Pages.ViewModels
{
    public class DocumentsPage : ComponentBase
    {
        public string? Path { get; set; }
        public string? Filter { get; set; }

        [Inject]
        public DocumentsViewModel DocumentsViewModel { get; set; } = null!;

        public IList<DocumentSearchResult> SearchResult { get; private set; } = new List<DocumentSearchResult>();

        public DocumentSearchResult? SelectedDocument { get; set; }

        public MudTable<DocumentSearchResult> ResultTable { get; set; } = null!;

        public string KeywordSearch { get; set; } = string.Empty;

        public Task ExtractWithTika()
        {
            if (Directory.Exists(Path))
            {
                DocumentsViewModel.ExtractPath(Path!, Filter!);
            }
            return Task.CompletedTask;
        }

        public Task StartServices()
        {
            return DocumentsViewModel.StartServices();
        }

        public async Task PerformSearch()
        {
            SearchResult = await DocumentsViewModel.SearchKeywordAsync(KeywordSearch);
        }

        public async Task PerformVectorSearch()
        {
            SearchResult = await DocumentsViewModel.SearchVectorAsync(KeywordSearch);
        }

        public string SelectedRowClassFunc(DocumentSearchResult item, int rowNumber)
        {
            if (item == SelectedDocument)
            {
                return "selected";
            }

            return string.Empty;
        }

        public void RowClickEvent(TableRowClickEventArgs<DocumentSearchResult> evt)
        {
            SelectedDocument = evt.Item;
        }

        public void ShowBtnPress(DocumentSearchResult document)
        {
            document.ShowDetails = !document.ShowDetails;
        }
    }
}
