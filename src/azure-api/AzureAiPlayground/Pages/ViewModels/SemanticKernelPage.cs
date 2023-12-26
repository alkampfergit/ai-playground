using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AzureAiPlayground.Pages.ViewModels;

public class SemanticKernelPage : ComponentBase
{
    [Inject]
    public SemanticKernelViewModel ViewModel { get; set; } = null!;

    public MudTable<StepViewModel> StepsTable { get; set; } = null!;

    public StepViewModel? SelectedStep { get; set; }

    public string SelectedRowClassFunc(StepViewModel item, int index)
    {
        if (item == SelectedStep)
        {
            return "selected";
        }

        return string.Empty;
    }

    public void RowClickEvent(TableRowClickEventArgs<StepViewModel> evt)
    {
        if (evt.Item == null) return;

        if (SelectedStep == evt.Item)
        {
            SelectedStep.Selected = false;
            return;
        }

        if (SelectedStep != null)
        {
            SelectedStep.Selected = false;
        }

        SelectedStep = evt.Item;

        if (SelectedStep != null)
        {
            SelectedStep.Selected = true;
        }
    }
}
