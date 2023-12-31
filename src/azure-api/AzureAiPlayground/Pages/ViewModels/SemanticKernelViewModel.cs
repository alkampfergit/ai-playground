using AzureAiLibrary.Helpers;
using AzureAiLibrary.Helpers.LogHelpers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using static AzureAiLibrary.Helpers.LogHelpers.DiagnoseResult;

namespace AzureAiPlayground.Pages.ViewModels;

public class SemanticKernelViewModel
{
    private readonly Kernel _kernel;
    private readonly DiagnoseHelper _diagnoseHelper;

    public SemanticKernelViewModel(
        Kernel kernel,
        DiagnoseHelper diagnoseHelper)
    {
        _kernel = kernel;
        _diagnoseHelper = diagnoseHelper;
    }

    public bool AnswerIsEmpty => String.IsNullOrEmpty(Answer);

    public string? Question { get; set; } = "I want to extract audio from video file C:\\temp\\230Github.mp4";

    public string? Answer { get; set; }

    public DiagnoseResult? Diagnostic { get; private set; }

    public List<StepViewModel> Steps = new();

    public async Task PerformQuestion()
    {
        Answer = "";
        Steps = new();
        if (String.IsNullOrEmpty(Question)) return;

        OpenAIPromptExecutionSettings openAiPromptExecutionSettings = new()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        };

        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        string correlationKey = Guid.NewGuid().ToString();
        DumpLoggingProvider.Instance.StartCorrelation(correlationKey);

        ChatHistory chatMessages = new();
        chatMessages.AddUserMessage(Question);
        var result = await chatCompletionService.GetChatMessageContentsAsync(
            chatMessages,
            executionSettings: openAiPromptExecutionSettings,
            kernel: _kernel);

        Answer = result.Last().Content;

        Diagnostic = _diagnoseHelper.Diagnose(correlationKey);
        if (Diagnostic != null)
        {
            Steps = Diagnostic.Steps.Select(s => new StepViewModel(s)).ToList();
        }
    }
}

public class StepViewModel
{
    public StepViewModel(Step step)
    {
        Step = step;
    }

    public bool Selected { get; set; }

    public Step Step { get; }
}
