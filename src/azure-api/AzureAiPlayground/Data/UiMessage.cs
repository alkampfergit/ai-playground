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
            message.ContentChanged += Message_ContentChanged;
            NumberOfLines = message.Content.Count(c => c == '\n');
        }

        private void Message_ContentChanged(object? sender, EventArgs e)
        {
            RefreshContentChanged();
        }

        public void RefreshContentChanged()
        {
            //update the fragments
            Fragments = ChatResponseParser.ParseApiResponse(Message.Content);
            NumberOfLines = Message.Content.Count(c => c == '\n');
            OnContentChanged(); //propagate the event to signal that the content chagned
        }

        public event EventHandler? ContentChanged;

        public void OnContentChanged()
        {
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        public AzureAiLibrary.Message Message { get; private set; }

        public List<TextFragment> Fragments { get; private set; }

        public int NumberOfLines { get; set; }
    }
}
