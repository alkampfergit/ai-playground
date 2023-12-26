namespace AzureAiPlayground.Controllers.Models;

public class SinglePrompt
{
    public string Question { get; set; }
}

public class SinglePromptResponse
{
    public static readonly SinglePromptResponse Empty = new();

    public string? Response { get; init; }

    public string? CorrelationKey { get; init; }
}