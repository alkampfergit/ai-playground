using AzureAiLibrary.Documents.DocumentChat;

namespace AzureAiPlayground.Controllers.Models;

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

    public static SegmentsSearch CreateSegmentsSearch(string indexName, IEnumerable<string>? docId = null, int numOfRecords = 10, string? keywords = null)
    {
        return new SegmentsSearch(indexName)
        {
            DocId = docId,
            NumOfRecords = numOfRecords,
            Keywords = keywords,
        };
    }

    public static SegmentsSearch ToSegmentSearch(this DocumentSegmentsQuestionsDto questionDto, string indexName)
    {
        var ss = new SegmentsSearch(indexName);
        if (questionDto.DocId?.Any() == true)
        {
            ss.DocId = questionDto.DocId;
        }
        ss.Keywords = questionDto.Keywords;
        return ss;
    }
}