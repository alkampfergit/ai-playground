using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Helpers;
using AzureAiPlayground.Controllers.Models;
using AzureAiPlayground.Support;
using Microsoft.AspNetCore.Mvc;

namespace AzureAiPlayground.Controllers
{
    [ApiController]
    [Route("/api/chat")]
    public class RawChatController : Controller
    {
        private readonly AzureOpenAiConfiguration _azureOpenAiConfiguration;
        private readonly TemplateHelper _templateHelper;
        private readonly ChatClient _chatClient;
        private readonly FolderDatabase<ApiPayload> _db;

        public RawChatController(
            FolderDatabaseFactory folderDatabaseFactory,
            TemplateHelper templateHelper,
            ChatClient chatClient,
            AzureOpenAiConfiguration azureOpenAiConfiguration)
        {
            _azureOpenAiConfiguration = azureOpenAiConfiguration;
            _templateHelper = templateHelper;
            _chatClient = chatClient;
            _db = folderDatabaseFactory.CreateDb<ApiPayload>();
        }

        [HttpPost]
        [Route("single-message")]
        public async Task<ActionResult> SingleMessage(ChatRequestMessage dto)
        {
            var message = _templateHelper.ExpandTemplates(dto.Message);
            var payload = new ApiPayload
            {
                Messages = new List<Message>()
                {
                    new Message("system", "You are an helpful AI"),
                    new Message("user", message),
                },
                MaxTokens = 800,
                Temperature = 0.5,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
                TopP = 0.95,
                Stop = null
            };
            var response = await _chatClient.SendMessageAsync(_azureOpenAiConfiguration.Default, payload);

            return Ok(response.Content);
        }

        [HttpPost]
        [Route("single-message-with-system")]
        public async Task<ActionResult> SingleMessageWithSystem(SingleChatMessage dto)
        {
            var message = _templateHelper.ExpandTemplates(dto.Message);
            var payload = new ApiPayload
            {
                Messages = new List<Message>()
                {
                    new Message("system", dto.System),
                    new Message("user", message),
                },
                MaxTokens = 100,
                Temperature = 0.5,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
                TopP = 0.95,
                Stop = null
            };
            var response = await _chatClient.SendMessageAsync(_azureOpenAiConfiguration.Default, payload);

            return Ok(response.Content);
        }

        /// <summary>
        /// Create a chat with some pre-defined user interactions.
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("send-message")]
        public ActionResult CreateChat(CreateChatDto dto)
        {
            var messages = new List<Message>();
            messages.Add(Message.CreateSystemMessage(dto.SystemMessage));

            for (int i = 0; i < dto.Context.Count; i++)
            {
                if (i % 2 == 0)
                {
                    messages.Add(Message.CreateUserMessage(dto.Context[i]));
                }
                else
                {
                    messages.Add(Message.CreateAssistantMessage(dto.Context[i]));
                }
            }

            var payload = new ApiPayload
            {
                Messages = messages,
                MaxTokens = 100,
                Temperature = 0.5,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
                TopP = 0.95,
                Stop = null
            };

            _db.Save(dto.ChatId, "", payload);

            return Ok();
        }

        [HttpPost]
        [Route("send-message")]
        public async Task<ActionResult> SendMessageToChat(AddChatMessage dto)
        {
            //load or re-create the first interaction
            var payload = _db.Load(dto.ChatId)?.Record ?? new ApiPayload
            {
                Messages = new List<Message>()
                {
                    new Message("system", "You are an AI assistant that helps people find information."),
                },
                MaxTokens = 100,
                Temperature = 0.5,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
                TopP = 0.95,
                Stop = null
            };

            var userMessage = Message.CreateUserMessage(dto.UserMessage);
            payload.Messages.Add(userMessage);
            var response = await _chatClient.SendMessageAsync(_azureOpenAiConfiguration.Default, payload);
            _db.Save(dto.ChatId, "", payload);

            return Ok(response.Content);
        }
    }
}
