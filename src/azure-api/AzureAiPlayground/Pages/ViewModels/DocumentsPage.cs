using Microsoft.AspNetCore.Components;

namespace AzureAiPlayground.Pages.ViewModels
{
    public class DocumentsPage : ComponentBase
    {
        public string? Path { get; set; }
        public string? Filter { get; set; }

        [Inject]
        public DocumentsViewModel DocumentsViewModel { get; set; } = null!;

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


        public class SearchResult 
        {
            public string Id { get; set; }
        }
    }
}
