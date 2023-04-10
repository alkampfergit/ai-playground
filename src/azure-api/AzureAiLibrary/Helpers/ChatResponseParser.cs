using System.Text;

namespace AzureAiLibrary.Helpers
{
    public static class ChatResponseParser
    {
        public static List<TextFragment> ParseApiResponse(string response)
        {
            List<TextFragment> fragments = new List<TextFragment>();

            string[] lines = response.Split('\n').Select(c => c.Trim('\n', '\r')).ToArray();
            bool isCodeSnippet = false;
            string currentLanguage = "";
            StringBuilder snippetBuilder = new StringBuilder();

            foreach (var line in lines)
            {
                if (line.StartsWith("```"))
                {
                    if (!isCodeSnippet) currentLanguage = line.Trim('`');
                    isCodeSnippet = !isCodeSnippet;

                    if (!isCodeSnippet)
                    {
                        fragments.Add(new TextFragment {
                            Content = snippetBuilder.ToString(), 
                            IsCodeSnippet = true,
                            Language = currentLanguage
                        });
                        snippetBuilder.Clear();
                    }
                    continue;
                }

                if (isCodeSnippet)
                {
                    snippetBuilder.AppendLine(line);
                }
                else
                {
                    fragments.Add(new TextFragment { Content = line, IsCodeSnippet = false });
                }
            }

            return fragments;
        }
    }
}
