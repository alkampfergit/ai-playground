using System.Text;
using Nest;
using TiktokenSharp;

namespace AzureAiLibrary.Documents;

public class Segmenter
{
    private readonly int _tokenLength;
    private readonly int _tokenOverlap;
    private readonly TikToken _tikTokTokenizer;

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
    public IReadOnlyCollection<string> Segment(IEnumerable<string> segments)
    {
        //first we split all segments with spaces, then store into an array
        //where we calculate the length in token of each word
        List<(string Word, int TokenCount)> words = new();
        foreach (var segment in segments)
        {
            var tokens = segment.Split(' ');
            foreach (var token in tokens)
            {
                var tokenCount = _tikTokTokenizer.Encode(token).Count;
                words.Add((token, tokenCount));
            }
        }
        
        //now we can iterate in all the words and create segments based
        //on length of the token precalculated.
        var result = new List<string>();
        var currentSegment = new StringBuilder();
        var currentTokenCount = 0;
        var index = 0;
        while (index < words.Count)
        {
            var word = words[index];
            if (currentTokenCount + word.TokenCount > _tokenLength)
            {
                //we need to create a new segment
                result.Add(currentSegment.ToString());
                currentSegment.Clear();
                currentTokenCount = 0;
                
                // ok we found a segment, now we need to go backward to create the overlap
                //because we want next segment to overlap with current
                var tokenToGoBack = _tokenOverlap;
                while (tokenToGoBack > 0 && index > 0)
                {
                    index--;
                    tokenToGoBack -= words[index].TokenCount;
                }

                continue;
            }

            currentSegment.Append(word.Word);
            currentSegment.Append(' ');
            currentTokenCount += word.TokenCount;
            index++;
        }

        if (currentTokenCount > _tokenOverlap)
        {
            //we need to create a new segment
            result.Add(currentSegment.ToString());
        }

        return result;
    }
}