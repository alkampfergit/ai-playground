using System.Diagnostics.CodeAnalysis;

namespace AzureAiPlayground.Controllers.Models;

public class SegmentedDocumentDto
{
    public required string DocumentId { get; set; }

    public required DocumentSegmentDto[] Segments { get; set; }
}

public class DocumentSegmentDto
{
    public required string Content { get; set; }
    public int PageId { get; set; }
    public string? Tag { get; set; }
}

public class SegmentsSearchDto
{
    public IEnumerable<string>? DocId { get; set; }

    public int NumOfRecords { get; set; } = 10;

    public string? Keywords { get; set; }
}

public class SegmentMatchDto
{
    public required string DocId { get; set; }

    public required string Content { get; set; }

    public int Page { get; set; }

    public string? Tag { get; set; }
}

public class DocumentSegmentsQuestionsDto
{
    /// <summary>
    /// Can restrict search in a specific document
    /// </summary>
    public IEnumerable<string>? DocId { get; set; }

    /// <summary>
    /// To help the system the user can specify kewords to be used to search for the answer.
    /// </summary>
    public string? Keywords { get; set; }

    public required string Question { get; set; }
}

public class DocumentSegmentsAnswerDto
{
    public required string Answer { get; set; }

    public IReadOnlyCollection<Citation>? Citations { get; set; }

    public required IReadOnlyCollection<DebugStep> DebugSteps { get; set; }
}

public class DebugStep
{
    [SetsRequiredMembers]
    public DebugStep(string title, string data)
    {
        Title=title;
        Data=data;
    }

    public required string Title { get; set; }

    public required string Data { get; set; }
}

public class Citation
{
    public string? DocId { get; set; }

    public string? Page { get; set; }

    public string? Tag { get; set; }
}

public class AnswerData
{
    public required string Answer { get; set; }

    public List<Citation>? Citations { get; set; }
}
