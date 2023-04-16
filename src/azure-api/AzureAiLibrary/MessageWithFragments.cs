using AzureAiLibrary.Helpers;

namespace AzureAiLibrary
{
    public class MessageWithFragments
    {
        public MessageWithFragments(Message message)
        {
            Message = message;
            Fragments = ChatResponseParser.ParseApiResponse(message.Content);
        }

        public Message Message { get; private set; }

        public List<TextFragment> Fragments { get; private set; }
    }
}
