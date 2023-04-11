using System.Text.RegularExpressions;

namespace AzureAiLibrary.Helpers
{
    public class TemplateHelper
    {
        private static Regex _templateRex = new Regex(@"\B@(\w+)\b", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        private readonly ITemplateManager _manager;

        public TemplateHelper(ITemplateManager manager)
        {
            _manager = manager;
        }

        public string ExpandTemplates(string? input)
        {
            if (String.IsNullOrEmpty(input)) return string.Empty;

            return _templateRex.Replace(input, match => GetTemplateContent(match.Groups[1].Value, match.Value));
        }

        private string GetTemplateContent(string templateName, string matchValue)
        {
            return _manager.GetTemplateContent(templateName) ?? matchValue;
        }
    }
}
