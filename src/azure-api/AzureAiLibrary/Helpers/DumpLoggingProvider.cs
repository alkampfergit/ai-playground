﻿using AzureAiLibrary.Helpers.LogHelpers;
using Microsoft.Extensions.Http.Logging;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace AzureAiLibrary.Helpers
{
    public class DumpLoggingProvider : ILoggerProvider
    {
        private readonly AccumulatorLogger _logger;
        private RequestBodyLogger _httpRequestBodyLogger;

        public static DumpLoggingProvider Instance = null!;

        public DumpLoggingProvider()
        {
            _logger = new AccumulatorLogger();
            Instance = this;
        }

        public void StartCorrelation(string correlationKey)
        {
            _logger.CorrelationKey.Value = correlationKey;
        }

        public IHttpClientAsyncLogger CreateHttpRequestBodyLogger(ILogger logger) =>
            _httpRequestBodyLogger = new RequestBodyLogger(logger);

        public ILogger CreateLogger(string categoryName)
        {
            return _logger;
        }

        public void Dispose()
        {
        }

        public IReadOnlyDictionary<string, LlmCallData> GetLLMCalls()
        {
            return _logger.GetLLMCalls();
        }

        public IEnumerable<LogInfo> GetLogs() => _logger.GetLogs();

        public record LlmCallData(string CorrelationKey, string Prompt, List<LLMCall> LlmCalls);

        class AccumulatorLogger : ILogger
        {
            private readonly List<LogInfo> _logs;
            private readonly Dictionary<string, LlmCallData> _llmCalls;

            internal AsyncLocal<string> CorrelationKey = new AsyncLocal<string>();

            public AccumulatorLogger()
            {
                _logs = new List<LogInfo>();
                _llmCalls = new Dictionary<string, LlmCallData>();
            }

            public IReadOnlyDictionary<string, LlmCallData> GetLLMCalls() => new ReadOnlyDictionary<string, LlmCallData>(_llmCalls);

            private string GetCorrelationValue() => CorrelationKey.Value ?? "global";

            public void AddLlmCall(LLMCall llmCall)
            {
                var cv = GetCorrelationValue();
                if (!_llmCalls.TryGetValue(cv, out var llmCallData))
                {
                    llmCallData = new LlmCallData(cv, llmCall.Prompt, new List<LLMCall> { llmCall });
                    _llmCalls[cv] = llmCallData;
                }
                else
                {
                    llmCallData.LlmCalls.Add(llmCall);
                }
            }

            internal LLMCall? CompleteLLMCall(string correlationId, string function, string arguments, string response)
            {
                var cv = GetCorrelationValue();
                if (_llmCalls.TryGetValue(cv, out var llmCallData))
                {

                    for (int i = llmCallData.LlmCalls.Count - 1; i >= 0; i--)
                    {
                        var llmCall = llmCallData.LlmCalls[i];
                        if (llmCall.CorrelationKey == correlationId)
                        {
                            llmCall.Response = response;
                            llmCall.ResponseFunctionCall = function;
                            llmCall.ResponseFunctionCallParameters = arguments;
                            return llmCall;
                        }
                    }
                }

                return null;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull
            {
                return new LogScope(state);
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                var stateDictionary = ExtractDictionaryFromState(state);
                var interfaces = state.GetType().GetInterfaces().Select(i => i.Name).ToList();
                LogInfo logInfo = new()
                {
                    CorrelationKey = GetCorrelationValue(),
                    Log = formatter(state, exception),
                    Parameters = stateDictionary
                };
                _logs.Add(logInfo);
            }

            private static Dictionary<string, string> ExtractDictionaryFromState<TState>(TState state)
            {
                Dictionary<string, string> retValue = new();
                if (state is IEnumerable en)
                {
                    foreach (var element in en)
                    {
                        if (element is KeyValuePair<string, object> stateValue)
                        {
                            retValue[stateValue.Key] = stateValue.Value?.ToString() ?? "";
                        }
                    }
                }
                return retValue;
            }

            public List<LogInfo> GetLogs()
            {
                return _logs;
            }
        }

        public class LogInfo 
        {
            public string CorrelationKey { get; set; }
            public string Log { get; set; }

            public Dictionary<string, string> Parameters { get; set; }
        }

        private class LogScope : IDisposable
        {
            private object _state;

            public LogScope(object state)
            {
                _state = state;
            }

            public void Dispose()
            {
            }
        }

        sealed class RequestBodyLogger(ILogger logger) : IHttpClientAsyncLogger
        {
            public async ValueTask<object?> LogRequestStartAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
            {
                var requestContent = await request.Content!.ReadAsStringAsync(cancellationToken);
                StringBuilder sb = new();

                if (request.RequestUri.Host.Contains("openai"))
                {
                    // I need to pase the request content as json object to extract some informations.
                    var jsonObject = JsonDocument.Parse(requestContent).RootElement;
                    var messages = jsonObject.GetProperty("messages");

                    sb.AppendLine($"Call LLM: {request.RequestUri}");

                    foreach (var message in messages.EnumerateArray())
                    {
                        var content = message.GetProperty("content").GetString();
                        sb.AppendLine($"{message.GetProperty("role").GetString()}: {content}");
                    }

                    if (jsonObject.TryGetProperty("tools", out var tools))
                    {

                        sb.AppendLine("Functions:");
                        foreach (JsonElement tool in tools.EnumerateArray())
                        {
                            // Extracting function object
                            JsonElement function = tool.GetProperty("function");

                            // Extracting function name and description
                            string functionName = function.GetProperty("name").GetString();
                            string functionDescription = function.GetProperty("description").GetString();

                            sb.AppendLine($"Function Name: {functionName}");
                            sb.AppendLine($"Description: {functionDescription}");

                            // Extracting parameters
                            JsonElement parameters = function.GetProperty("parameters");
                            foreach (JsonProperty parameter in parameters.EnumerateObject())
                            {
                                sb.AppendLine($"Parameter name {parameter.Name} Value; {parameter.Value}");
                            }
                            sb.AppendLine();
                        }
                    }
                    foreach (var header in request.Headers)
                    {
                        if (!header.Key.Contains("key", StringComparison.OrdinalIgnoreCase))
                        {
                            sb.AppendLine($"{header.Key}: {header.Value.First()}");
                        }
                    }

                    LLMCall lLMCall = new LLMCall()
                    {
                        CorrelationKey = request.Headers.GetValues("x-ms-client-request-id").First(),
                        Prompt = jsonObject.GetProperty("messages").ToString(),
                        Request = requestContent,
                        PromptFunctions = tools.ToString()
                    };
                    DumpLoggingProvider.Instance._logger.AddLlmCall(lLMCall);
                }
                else
                {
                    sb.AppendLine($"Call HTTP: {request.RequestUri}");
                    sb.AppendLine("CONTENT:");
                    sb.AppendLine(requestContent);
                }

                logger.LogTrace(sb.ToString());
                return default;
            }

            public object? LogRequestStart(HttpRequestMessage request)
            {
                var requestContent = request.Content!.ReadAsStringAsync().Result;

                logger.LogTrace("Request: {Request}", request);
                logger.LogTrace("Request content: {Content}", requestContent);
                return default;
            }

            public void LogRequestStop(object? context, HttpRequestMessage request, HttpResponseMessage response, TimeSpan elapsed)
            {
                var responseContent = response.Content.ReadAsStringAsync().Result;
                logger.LogTrace("Response: {Response}", response);
                logger.LogTrace("Response content: {Content}", responseContent);
            }
            public ValueTask LogRequestStopAsync(object? context, HttpRequestMessage request, HttpResponseMessage response, TimeSpan elapsed, CancellationToken cancellationToken = default)
            {
                var responseContent = response.Content.ReadAsStringAsync().Result;

                var sb = new StringBuilder();
                var functions = GetFunctionInformation(responseContent);
                if (functions.Function != null)
                {
                    sb.AppendLine($"Call function {functions.Function} with arguments {functions.Arguments}");
                }

                sb.AppendLine($"Response: {response}");
                sb.AppendLine($"Response content: {responseContent}");

                if (request.RequestUri.Host.Contains("openai"))
                {
                    var correlationId = response.Headers.GetValues("x-ms-client-request-id").First();
                    var llmCall = DumpLoggingProvider.Instance._logger.CompleteLLMCall(correlationId, functions.Function, functions.Arguments, responseContent);
                    if (llmCall != null)
                    {
                        logger.LogTrace(llmCall.Dump());
                        return ValueTask.CompletedTask;
                    }
                }

                logger.LogTrace(sb.ToString());
                return ValueTask.CompletedTask;
            }

            private static (string Function, string Arguments) GetFunctionInformation(string responseContent)
            {
                try
                {
                    var root = JsonDocument.Parse(responseContent);
                    var functionInfo = root.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("tool_calls")[0]
                        .GetProperty("function");

                    string functionName = functionInfo.GetProperty("name").GetString();
                    string arguments = functionInfo.GetProperty("arguments").GetString();

                    return (functionName, arguments);
                }
                catch (Exception)
                {
                    return (null, null);
                }
            }

            public void LogRequestFailed(object? context, HttpRequestMessage request, HttpResponseMessage? response, Exception exception, TimeSpan elapsed) { }
            public ValueTask LogRequestFailedAsync(object? context, HttpRequestMessage request, HttpResponseMessage? response, Exception exception, TimeSpan elapsed, CancellationToken cancellationToken = default) => default;
        }
    }
}
