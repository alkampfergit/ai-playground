using AzureAiLibrary;
using AzureAiLibrary.Helpers;

namespace AzureAiPlayground.Data
{
    public class UiMessage
    {
        public UiMessage(Message message)
        {
            Message = message;
            Fragments = ChatResponseParser.ParseApiResponse(message.Content);
        }

        public AzureAiLibrary.Message Message { get; private set; }

        public List<TextFragment> Fragments { get; private set; }
    }
}
