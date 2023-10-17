using AzureAiLibrary.Documents.DocumentChat;

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

public static class SegmentsSearchDtoExtensions
{
    public static SegmentsSearch ToSegmentsSearch(this SegmentsSearchDto dto, string indexName)
    {
        return new SegmentsSearch(indexName)
        {
            DocId = dto.DocId,
            NumOfRecords = dto.NumOfRecords,
            Keywords = dto.Keywords,
        };
    }
}
