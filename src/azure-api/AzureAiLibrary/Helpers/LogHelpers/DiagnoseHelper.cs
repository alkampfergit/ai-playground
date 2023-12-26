namespace AzureAiLibrary.Helpers.LogHelpers
{
    public class DiagnoseHelper
    {
        private readonly DumpLoggingProvider _dumpLoggingProvider;

        public DiagnoseHelper(DumpLoggingProvider dumpLoggingProvider)
        {
            _dumpLoggingProvider = dumpLoggingProvider;
        }

        public DiagnoseResult? Diagnose(string correlationKey)
        {
            var llmCalls = _dumpLoggingProvider.GetLLMCalls();
            if (!llmCalls.TryGetValue(correlationKey, out var llmCallInformations))
            {
                return null;
            }

            var parser = new OpenAICallParser();
            DiagnoseResult diagnoseResult = new DiagnoseResult();
            diagnoseResult.Question = llmCallInformations.Prompt;
            foreach (var llmCall in llmCallInformations.LlmCalls)
            {
                var parsedRequest = parser.ParseRequest(llmCall.Request);
                var parsedResponse = parser.ParseResponse(llmCall.Response);
                var step = new DiagnoseResult.Step();
                step.Prompt = parsedRequest.PromptSequence;
                step.Model = parsedResponse.Model;
                step.AnswerType = parsedResponse.FinishReason;
                if (parsedResponse.FinishReason == "tool_calls")
                {
                    step.Answer = $"Call tool {parsedResponse.FunctionCall} with arguments ({parsedResponse.FunctionArguments})";
                }
                else if (parsedResponse.FinishReason == "stop")
                {
                    step.Answer = $"Assistant stopped, answer: {parsedResponse.Answer}";
                }

                step.PromptTokens = parsedResponse.PromptTokens;
                step.AnswerTokens = parsedResponse.AnswerTokens;
                step.TotalTokens = parsedResponse.TotalTokens;
                step.FunctionCall = parsedResponse.FunctionCall;
                step.FunctionArguments = parsedResponse.FunctionArguments;

                step.FullRequest = JsonHelper.Beautify(llmCall.Request);
                step.FullResponse = JsonHelper.Beautify( llmCall.Response);
                diagnoseResult.Steps.Add(step);
            }

            diagnoseResult.TotalUsedTokens = diagnoseResult.Steps
                .GroupBy(s => s.Model)
                .Select(s => new ModelUsedTokens(s.Key, s.Sum(m => m.TotalTokens)))
                .ToArray();

            return diagnoseResult;
        }
    }

    public class DiagnoseResult
    {
        public string Question { get; internal set; }

        public List<Step> Steps { get; set; } = new List<Step>();
        public ModelUsedTokens[] TotalUsedTokens { get; internal set; }

        public class Step
        {
            public string Prompt { get; internal set; }
            public string AnswerType { get; internal set; }
            public string Answer { get; internal set; }
            public string Model { get; internal set; }
            public int TotalTokens { get; internal set; }
            public int PromptTokens { get; internal set; }
            public int AnswerTokens { get; internal set; }
            public string FunctionCall { get; internal set; }
            public string FunctionArguments { get; internal set; }
            public string FullResponse { get; internal set; }
            public string FullRequest { get; internal set; }
        }
    }

    public record ModelUsedTokens(string Model, int TotalUsedTokens);
}
