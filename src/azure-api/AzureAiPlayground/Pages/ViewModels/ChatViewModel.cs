﻿using AzureAiLibrary.Configuration;
using Microsoft.Extensions.Options;
using Endpoint = AzureAiLibrary.Configuration.Endpoint;

namespace AzureAiPlayground.Pages.ViewModels
{
    using AzureAiLibrary;
    using AzureAiLibrary.Helpers;
    using AzureAiPlayground.Data;
    using AzureAiPlayground.Support;
    using Microsoft.AspNetCore.Connections.Features;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class ChatViewModel
    {
        public ChatUi ChatUi { get; set; } = null!;

        public string Description { get; set; } = "A Chat";

        public string UserInput { get; set; } = "";

        public bool IsLoading { get; set; }

        public bool IsInEditMode { get; set; }

        public IEnumerable<Endpoint> Endpoints { get; private set; }

        public Endpoint SelectedEndpoint { get; set; }

        private FolderDatabase<ChatUi> _db;
        private readonly ChatClient _chatClient;
        private readonly IOptionsMonitor<AzureOpenAiConfiguration> _azureOpenAiConfiguration;

        public string Id { get; set; } = null!;

        public ChatViewModel(
            ChatClient chatClient,
            FolderDatabaseFactory folderDatabaseFactory,
            IOptionsMonitor<AzureOpenAiConfiguration> azureOpenAiConfiguration)
        {
            _db = folderDatabaseFactory.CreateDb<ChatUi>();
            _chatClient = chatClient;
            _azureOpenAiConfiguration = azureOpenAiConfiguration;

            Endpoints = azureOpenAiConfiguration.CurrentValue.Endpoints;
            SelectedEndpoint = azureOpenAiConfiguration.CurrentValue.GetDefaultEndpoint();
        }
        
        public event EventHandler? ContentChanged;

        public void OnContentChanged()
        {
            ContentChanged?.Invoke(this, EventArgs.Empty);
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

                var response = await _chatClient.SendMessageStreamingAsync(SelectedEndpoint.Name, payload);
                //var response = await _chatClient.SendMessageAsync(SelectedEndpoint.Name, payload);
                ChatUi.Messages.Add(new UiMessage(userMessage));
                var responseMessage = new UiMessage(response);
                responseMessage.ContentChanged += (s, e) => OnContentChanged();
                ChatUi.Messages.Add(responseMessage);

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

        public Task AddEmpty()
        {
            var lastMessage = ChatUi.Messages.LastOrDefault();

            //add an empty message with a placeholder
            var role = lastMessage?.Message.Role == "user" ? "assistant" : "user";
            UiMessage newMessage = new UiMessage(new Message { Role = role, Content = "Edit the message here" });
            newMessage.NumberOfLines = 10; //give space.
            ChatUi.Messages.Add(newMessage);
            return Task.CompletedTask;
        }

        public Task ToggleEdit()
        {
            if (IsInEditMode) 
            {
                //update each message in chat
                foreach (var message in ChatUi.Messages) { message.RefreshContentChanged(); }
            }
            IsInEditMode = !IsInEditMode;
            return Task.CompletedTask;
        }
    }
}
