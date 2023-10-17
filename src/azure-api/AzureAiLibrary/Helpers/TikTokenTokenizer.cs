using TiktokenSharp;

namespace AzureAiLibrary.Helpers
{
    public static class TikTokenTokenizer
    {
        private static TikToken _tokenizer = TikToken.GetEncoding("cl100k_base");

        public static int GetTokenCount(string text)
        {
            return _tokenizer.Encode(text).Count;
        }
    }
}
