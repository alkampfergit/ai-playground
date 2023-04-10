using AzureAiLibrary;

namespace AzureAiPlayground.Data
{
    public class ChatUi
    {
        public ChatUi()
        {
            Parameters = new Parameters()
            {
                MaxResponse = 1000,
                Temperature = 0.5,
                TopP = 0.95,
            };
            Setup = "You are an AI helpful assistant.";
            Messages = new();
        }

        public Parameters  Parameters { get; set; }

        public List<UiMessage> Messages { get; set; }

        public string Setup { get; set; }

        public bool Favorited { get; set; }
    }
}
