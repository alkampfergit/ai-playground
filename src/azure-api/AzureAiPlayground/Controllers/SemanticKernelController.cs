using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Helpers;
using AzureAiPlayground.Agents;
using AzureAiPlayground.Controllers.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;

namespace AzureAiPlayground.Controllers
{
    [ApiController]
    [Route("/api/semantickernel")]
    public class SemanticKernelController : Controller
    {
        private readonly Kernel _kernel;
        private readonly DumpLoggingProvider _loggingProvider;
        private readonly IOptionsMonitor<AzureOpenAiConfiguration> _azureOpenAiConfiguration;

        public SemanticKernelController(
            Kernel kernel,
            DumpLoggingProvider loggingProvider,
            IOptionsMonitor<AzureOpenAiConfiguration> azureOpenAiConfiguration)
        {
            _kernel = kernel;
            _loggingProvider = loggingProvider;
            _azureOpenAiConfiguration = azureOpenAiConfiguration;
        }

        [HttpGet]
        [Route("read-dump")]
        public ActionResult SingleMessage()
        {
            var logs = _loggingProvider.GetLogs();
            return Ok(_loggingProvider.GetLogs());
        }
        
        [HttpPost]
        [Route("prompt")]
        public async Task<SinglePromptResponse> Prompt(SinglePrompt message)
        {
            OpenAIPromptExecutionSettings openAiPromptExecutionSettings = new()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            };

            var chatPrompt = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "SemanticKernel", "Prompts", "Chat.yaml");
            var promptContent = System.IO.File.ReadAllText(chatPrompt);
            KernelFunction prompt = _kernel.CreateFunctionFromPromptYaml(
                promptContent,
                promptTemplateFactory: new HandlebarsPromptTemplateFactory()
            );

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();



            ChatHistory chatMessages = new();
            chatMessages.AddUserMessage(message.Question);
            var result = await chatCompletionService.GetChatMessageContentsAsync(
                chatMessages,
                executionSettings: openAiPromptExecutionSettings,
                kernel: _kernel);

            return new SinglePromptResponse()
            {
                Response = result.Last().Content!
            };
        }
    }
}
