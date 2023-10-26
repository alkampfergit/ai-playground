
using AzureAiLibrary.Configuration;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace AzureAiLibrary.Helpers
{
    public interface ITemplateManager
    {
        /// <summary>
        /// Get raw template from the directory.
        /// </summary>
        /// <param name="templateName"></param>
        /// <returns></returns>
        string GetTemplateContent(string templateName);

        (string SystemMessage, string Prompt) GetGptCallTemplate(string templateName, Dictionary<string, string> variables);
    }

    public class DefaultTemplateManager : ITemplateManager
    {
        private readonly IOptionsMonitor<ChatConfig> _config;

        public static readonly Regex AngularReplaceRegex = new Regex("{(?<pattern>.+?)}", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);


        public DefaultTemplateManager(IOptionsMonitor<ChatConfig> config)
        {
            _config = config;
        }

        public (string SystemMessage, string Prompt) GetGptCallTemplate(
            string templateName,
            Dictionary<string, string> variables)
        {
            var template = _config.CurrentValue.GetTemplateContent(templateName);
            //system message is the first part of the lines up to an empty string
            //the rest is the prompt
            var lines = template.Split('\n').Select(l => l.Trim('\r', '\n')).ToArray();

            //combine up to an empty string
            var systemMessage = string.Join(Environment.NewLine, lines.TakeWhile(l => !string.IsNullOrEmpty(l)));
            var prompt = string.Join(Environment.NewLine, lines
                .SkipWhile(l => !string.IsNullOrEmpty(l))
                .Skip(1));

            //now I need to identify all {token} inside both system message and prompt and substitute with the corresponding entry
            //in the dictionary variables
            systemMessage = AngularReplaceRegex.Replace(
                systemMessage,
                m => AngularTemplateLikeReplaceMatchEvaluator(m, variables));

            prompt = AngularReplaceRegex.Replace(
                prompt,
                m => AngularTemplateLikeReplaceMatchEvaluator(m, variables));

            return (systemMessage, prompt);
        }

        private static string AngularTemplateLikeReplaceMatchEvaluator(
            Match match,
            IReadOnlyDictionary<string, string> propertyMap)
        {
            var groupValue = match.Groups["pattern"].Value;
            if (propertyMap.TryGetValue(groupValue, out var token))
            {
                return token;
            }
            return match.Value;
        }

        public string GetTemplateContent(string templateName)
        {
            return _config.CurrentValue.GetTemplateContent(templateName) ?? string.Empty;
        }
    }
}
