using AzureAiLibrary.Helpers;
using AzureAiLibrary.Helpers.LogHelpers;
using AzureAiPlayground.Controllers.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AzureAiPlayground.Controllers
{
    [ApiController]
    [Route("/api/semantickernel")]
    public class SemanticKernelController : Controller
    {
        private readonly Kernel _kernel;
        private readonly DiagnoseHelper _diagnoseHelper;
        private readonly DumpLoggingProvider _loggingProvider;

        public SemanticKernelController(
            Kernel kernel,
            DiagnoseHelper diagnoseHelper,
            DumpLoggingProvider loggingProvider)
        {
            _kernel = kernel;
            _diagnoseHelper = diagnoseHelper;
            _loggingProvider = loggingProvider;
        }

        [HttpGet]
        [Route("read-dump")]
        public ActionResult ReadDump()
        {
            var logs = _loggingProvider.GetLogs();
            return Ok(logs);
        }

        [HttpGet]
        [Route("diagnose/{correlationKey}")]
        public ActionResult Diagnose(string correlationKey)
        {
            var diagnoseResult = _diagnoseHelper.Diagnose(correlationKey);
            if (diagnoseResult == null)
            {
                return NotFound();
            }
            return Ok(diagnoseResult);
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

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            string correlationKey = Guid.NewGuid().ToString();
            DumpLoggingProvider.Instance.StartCorrelation(correlationKey);

            ChatHistory chatMessages = new();
            chatMessages.AddUserMessage(message.Question);
            var result = await chatCompletionService.GetChatMessageContentsAsync(
                chatMessages,
                executionSettings: openAiPromptExecutionSettings,
                kernel: _kernel);

            return new SinglePromptResponse()
            {
                Response = result.Last().Content!,
                CorrelationKey = correlationKey,
            };
        }
    }
}
