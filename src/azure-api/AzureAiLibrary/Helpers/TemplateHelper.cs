using System.Text.RegularExpressions;

namespace AzureAiLibrary.Helpers
{
    public partial class TemplateHelper
    {
        private static readonly Regex _templateRex = TemplateRegex();

        [GeneratedRegex("\\B@(\\w+)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline, "it-IT")]
        private static partial Regex TemplateRegex();

        private readonly ITemplateManager _manager;

        public TemplateHelper(ITemplateManager manager)
        {
            _manager = manager;
        }

        /// <summary>
        /// Takes a string that contains some template tokens. It will fill that template tokens with text
        /// that is inside the template text. Template is like @(test) that will be expanded using text 
        /// from template test.txt
        /// </summary>
        /// <param name="input"></param>
        public string ExpandTemplates(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            return _templateRex.Replace(input, match => GetTemplateContent(match.Groups[1].Value, match.Value));
        }

        private string GetTemplateContent(string templateName, string matchValue)
        {
            return _manager.GetTemplateContent(templateName) ?? matchValue;
        }
    }
}
