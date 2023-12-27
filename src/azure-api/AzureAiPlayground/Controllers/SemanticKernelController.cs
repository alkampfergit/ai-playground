using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Helpers;
using AzureAiPlayground.Agents;
using AzureAiPlayground.Controllers.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

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
    }
}
