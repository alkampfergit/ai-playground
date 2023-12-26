﻿namespace AzureAiLibrary.Helpers.LogHelpers
{
    public class LLMCall
    {
        public string CorrelationKey { get; set; }

        public string Prompt { get; set; }

        public string PromptFunctions { get; set; }

        public string Response { get; set; }

        public string ResponseFunctionCall { get; set; }

        public string ResponseFunctionCallParameters { get; set; }

        public string Dump()
        {
            if (string.IsNullOrEmpty(PromptFunctions))
                return
                    $"Prompt: {Prompt}\n" +
                    $"Response: {Response}\n" +
                    $"ResponseFunctionCall: {ResponseFunctionCall}\n";

            return $"Ask to LLM: {Prompt} -> Call function {ResponseFunctionCall} with arguments {ResponseFunctionCallParameters}";
        }
    }
}
