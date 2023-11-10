using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Helpers;
using AzureAiPlayground.Agents;
using AzureAiPlayground.Controllers.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureAiPlayground.Controllers
{
    [ApiController]
    [Route("/api/workflow")]
    public class WorkflowController : Controller
    {
        private readonly IOptionsMonitor<AzureOpenAiConfiguration> _azureOpenAiConfiguration;
        private readonly IEnumerable<IAgent> _agents;
        private readonly TemplateHelper _templateHelper;
        private readonly ChatClient _chatClient;

        public WorkflowController(
            TemplateHelper templateHelper,
            ChatClient chatClient,
            IOptionsMonitor<AzureOpenAiConfiguration> azureOpenAiConfiguration,
            IEnumerable<IAgent> agents)
        {
            _azureOpenAiConfiguration = azureOpenAiConfiguration;
            _agents = agents;
            _templateHelper = templateHelper;
            _chatClient = chatClient;
        }

        [HttpPost]
        [Route("execute")]
        public async Task<ActionResult> SingleMessage(WorkflowData dto)
        {
            var agent = _agents.First();
            var result = await agent.Execute(new Dictionary<string, string>()
            {
                ["context"] = dto.Context 
            }, 
            dto.Message);
            return Ok(result);
        }
    }
}
