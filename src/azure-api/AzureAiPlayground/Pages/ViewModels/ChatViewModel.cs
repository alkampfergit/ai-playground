namespace AzureAiPlayground.Pages.ViewModels
{
    using AzureAiLibrary;
    using AzureAiLibrary.Helpers;
    using AzureAiPlayground.Data;
    using AzureAiPlayground.Support;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class ChatViewModel
    {
        public ChatUi ChatUi { get; set; }

        public string Description { get; set; } = "A Chat";

        public string UserInput { get; set; } = "";

        public bool IsLoading { get; set; }

        private FolderDatabase<ChatUi> _db;
        private readonly ChatClient _chatClient;

        public string Id { get; set; }

        public ChatViewModel(
            ChatClient chatClient,
            FolderDatabaseFactory folderDatabaseFactory)
        {
            _db = folderDatabaseFactory.CreateDb<ChatUi>();
            _chatClient = chatClient;
        }

        internal void Initialize(string id)
        {
            Id = id;
            if (!String.IsNullOrWhiteSpace(Id))
            {
                var dbEntry = _db.Load(Id);
                ChatUi = dbEntry.Record ?? new ChatUi();
                Description = dbEntry.Description;
            }
            else
            {
                InitNewChat();
            }
        }

        private void InitNewChat()
        {
            Id = Guid.NewGuid().ToString();
            Description = "A Chat";
            ChatUi = new();
        }

        public async Task SendMessage()
        {
            if (!string.IsNullOrWhiteSpace(UserInput) && !IsLoading)
            {
                IsLoading = true;
                var userMessage = new Message { Role = "user", Content = UserInput };
                var dataToSend = new[]
                {
                    new Message {
                        Role = "system",
                        Content = ChatUi.Setup
                    }
                }.Union(ChatUi.Messages.Select(m => m.Message))
                .Union(new[] { userMessage })
                .ToList();

                var payload = new ApiPayload
                {
                    Messages = dataToSend,
                    MaxTokens = ChatUi.Parameters.MaxResponse,
                    Temperature = ChatUi.Parameters.Temperature,
                    TopP = ChatUi.Parameters.TopP,
                    FrequencyPenalty = 0,
                    PresencePenalty = 0,
                    Stop = null
                };

                var response = await _chatClient.SendMessageAsync(payload);
                ChatUi.Messages.Add(new MessageWithFragments(userMessage));
                ChatUi.Messages.Add(new MessageWithFragments(response));

                UserInput = "";
                IsLoading = false;
                Save();
            }
        }

        public void ToggleFavorite()
        {
            ChatUi.Favorited = !ChatUi.Favorited;
            Save();
        }

        internal void Save()
        {
            _db.Save(Id, Description, ChatUi);
        }

        internal void Delete()
        {
            _db.Delete(Id);
            InitNewChat();
        }
    }
}
