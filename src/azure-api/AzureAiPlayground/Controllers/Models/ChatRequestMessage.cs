namespace AzureAiPlayground.Controllers.Models
{
    public class ChatRequestMessage
    {
        public required string Message { get; set; }
    }

    public class SingleChatMessage
    {
        public required string System { get; set; }
        public required string Message { get; set; }
    }

    /// <summary>
    /// If you want to create a chat with a specific system message and maybe
    /// some pre-existing messages, you can use this request.
    /// </summary>
    public class CreateChatDto
    {
        /// <summary>
        /// This is the id of the chat, it has meaning only for the caller
        /// </summary>
        public required string ChatId { get; set; }

        /// <summary>
        /// System message to initialize the chat.
        /// </summary>
        public required string SystemMessage { get; set; }

        /// <summary>
        /// List of messages, the first one is user message, then ai message and so on.
        /// </summary>
        public required List<string> Context { get; set; }
    }

    /// <summary>
    /// Add a message to an existing chat, it will be added to the end of the chat
    /// </summary>
    public class AddChatMessage
    {
        /// <summary>
        /// This is the id of the chat, it has meaning only for the caller
        /// </summary>
        public required string ChatId { get; set; }

        /// <summary>
        /// New user message to add to the chat.
        /// </summary>
        public required string UserMessage { get; set; }
    }
}
