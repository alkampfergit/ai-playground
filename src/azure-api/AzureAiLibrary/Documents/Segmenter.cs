using System.Text;
using Nest;
using TiktokenSharp;

namespace AzureAiLibrary.Documents;

public class Segmenter
{
    private readonly int _tokenLength;
    private readonly int _tokenOverlap;
    private readonly TikToken _tikTokTokenizer;

    /// <summary>
    /// This is the class that contains a segmented result.
    /// </summary>
    /// <param name="Content"></param>
    /// <param name="Index"></param>
    /// <param name="TokenCount"></param>
    public record SegmentInfo(string Content, int Index, int TokenCount)
    {
        public ElasticDocument ToElasticDocument(string docId)
        {
            var doc = new ElasticDocument(Guid.NewGuid().ToString());
            doc.AddTextProperty("content", this.Content);
            doc.AddNumericProperty("page", this.Index);
            doc.AddStringProperty("docid", docId);
            return doc;
        }
    }

    public Segmenter(int tokenLength, int tokenOverlap)
    {
        _tokenLength = tokenLength;
        _tokenOverlap = tokenOverlap;
        
        _tikTokTokenizer = TikToken.GetEncoding("cl100k_base");
    }
    
    /// <summary>
    /// Extract a series of segments from an array of strings, where each string
    /// is a piece of text. It will use _segmentLength and _segmentOverlap to create
    /// segments approximately the length in token as _segmentLength, with an overlap
    /// of _segmentOverlap between original segments.
    /// Word are split by space.
    /// </summary>
    /// <param name="segments">Segmented text</param>
    /// <returns></returns>
    public IReadOnlyCollection<SegmentInfo> Segment(IEnumerable<string> segments)
    {
        //first we split all segments with spaces, then store into an array
        //where we calculate the length in token of each word
        List<(string Word, int TokenCount, int index)> words = new();
        //convert next foreach to for
        int index = 0;
        foreach (var segment in segments)
        {
            //split the word using spaces and carriage return
            //also we have bad words that are too long (symbols sequences)
            var tokens = segment
                .Split(' ','\n')
                .Select(s => s.Trim(' ', '\r', '\n'))
                .Where(t => t.Length < 30);
            
            foreach (var token in tokens)
            {
                var tokenCount = _tikTokTokenizer.Encode(token).Count;
                words.Add((token, tokenCount, index));
            }

            index++;
        }
        
        //now we can iterate in all the words and create segments based
        //on length of the token precalculated.
        var result = new List<SegmentInfo>();
        var currentSegment = new StringBuilder();
        var currentTokenCount = 0;
        index = 0;
        while (index < words.Count)
        {
            var word = words[index];
            currentSegment.Append(word.Word);
            if (currentTokenCount + word.TokenCount > _tokenLength)
            {
                //we need to create a new segment
                result.Add(new SegmentInfo(currentSegment.ToString(), word.index, currentTokenCount));
                currentSegment.Clear();
                currentTokenCount = 0;

                // ok we found a segment, now we need to go backward to create the overlap
                //because we want next segment to overlap with current
                //we cannot come back more than the starting point because we could end in a loop
                //this can happen if the text has some anomalies like a very long word
                if (index == words.Count - 1)
                {
                    //we are at the end of the text, we cannot go back
                    break;
                };
                var tokenToGoBack = _tokenOverlap;
                while (tokenToGoBack > 0 && index > 0)
                {
                    index--;
                    //beware of long tokens
                    var tokenLength = words[index].TokenCount;
                    if (tokenLength > _tokenOverlap)
                    {
                       //token too long, skip
                       index++;
                       tokenToGoBack = 0;
                    }
                    else
                    {
                        tokenToGoBack -= words[index].TokenCount;
                    }
                }

                continue;
            }
            
            currentSegment.Append(' ');
            currentTokenCount += word.TokenCount;
            index++;
        }

        if (currentTokenCount > _tokenOverlap)
        {
            //we need to create a new segment
            result.Add(new SegmentInfo(currentSegment.ToString(), words[^1].index, currentTokenCount));
        }

        return result;
    }
}