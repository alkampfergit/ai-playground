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
            message.ContentChanged+= Message_ContentChanged;
        }

        private void Message_ContentChanged(object? sender, EventArgs e)
        {
            //update the fragments
            Fragments = ChatResponseParser.ParseApiResponse(Message.Content);
            OnContentChanged(); //propagate the event to signal that the content chagned
        }
        
        public event EventHandler ContentChanged;

        public void OnContentChanged()
        {
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        public AzureAiLibrary.Message Message { get; private set; }

        public List<TextFragment> Fragments { get; private set; }
    }
}
