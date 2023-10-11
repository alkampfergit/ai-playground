using AzureAiLibrary.Documents;

namespace AzureAiLibrary.Tests.Documents;

public class SegmenterTests
{
    [Theory]
    [InlineData(
        "lorem",
        10, 10, 1)]
    [InlineData(
        "lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et dolore magna aliqua",
        10, 0, 4)]
     [InlineData(
        "lorem\nipsum\ndolor\nsit\namet\nconsectetur\nadipiscing\nelit\nsed\ndo\neiusmod\ntempor\nincididunt\nut\nlabore\net dolore magna aliqua",
        10, 0, 4)]
    [InlineData(
        "lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et dolore magna aliqua",
        15, 5, 4)]
    [InlineData(
        "lorem ipsum dolor sit amet consectetur adipiscingelitseddoeiusmodtemporincididuntutlaboreetdoloremagnaaliqua",
        10, 5, 2)]
    [InlineData(
        "American Acropolis is a science fiction novel by William Gibson, published in 1999. The story follows Colin Laney, a man who has the power to see \"nodal points\" in the vast streams of data that make up the worldwide computer network. Nodal points are rare but significant events in history that forever change society, even though they might not be recognizable as such when they occur. Colin isn't quite sure what's going to happen when society reaches this latest nodal point, but he knows it's going to be big. And he knows it's going to occur on the Bay Bridge in San Francisco, which has been home to a sort of SoHo-esque shantytown since an earthquake rendered it structurally unsound to carry traffic. The novel is set in a dystopian future where the world is dominated by corporations and the internet has evolved into a vast, all-encompassing network known as the \"matrix\". Colin Laney is a \"nodal point\" tracker, a person who can see the future by analyzing data patterns. He is hired by a mysterious woman named Cody Harwood to track down a man named Rei Toei, who is rumored to be the key to a new form of artificial intelligence. Along the way, Laney meets a cast of characters, including a former rock star turned corporate assassin, a Japanese gangster, and a group of hackers who are trying to take down the corporations that control the matrix. As Laney delves deeper into the mystery of Rei Toei, he discovers that the key to the new AI lies in the Bay Bridge shantytown. The shantytown is home to a group of people who have been living off the grid, outside the reach of the corporations that control the rest of the world. Laney must navigate the dangerous world of corporate espionage and deal with his own personal demons if he hopes to uncover the truth about Rei Toei and the new AI.",
        100, 15, 6)]
    public void TestSegmenterBase(string data, int segmentLength, int overlap, int expectedNumberOfSegments)
    {
        var sut = new Segmenter(segmentLength, overlap);
        var segments = sut.Segment(new [] {data});
        Assert.Equal(expectedNumberOfSegments, segments.Count);
    }

    [Fact]
    public void Filter_out_symbol_word() 
    {
        var sut = new Segmenter(100, 10);
        var segments = sut.Segment(new[] { "this contains word with ++++symbols++++ that must be excluded" });
        Assert.Equal("this contains word with that must be excluded", segments.Single().Content);
    }
}