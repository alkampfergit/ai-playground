namespace AzureAiLibrary.Configuration
{
    /// <summary>
    /// General configuration for the chat.
    /// </summary>
    public class ChatConfig
    {
        public string DataDir { get; set; }

        public string TemplateDir { get; set; }

        /// <summary>
        /// Get content of template file.
        /// </summary>
        /// <param name="templateName"></param>
        /// <returns></returns>
        public string? GetTemplateContent(string templateName)
        {
            List<string> candidateFiles = new()
            {
                Path.Combine(TemplateDir, templateName),
                Path.Combine(TemplateDir, templateName, ".txt"),
            };
            foreach (var candidate in candidateFiles)
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            return null;
        }
    }
}
