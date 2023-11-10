using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Helpers;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AzureAiPlayground.Agents
{
    public class ApplyRuleAgent : IAgent
    {
        private readonly ChatClient _chatClient;
        private readonly JarvisApiCaller _jarvisApiCaller;
        private readonly IOptionsMonitor<JarvisConfig> _jarvisOptions;
        private readonly ITemplateManager _templateManager;
        private readonly string getTaskUrl = "workspaces/api/public/worktask/{WORKSPACEID}/search?api-version=2-preview";
        private readonly string getDetailUrl = "workspaces/api/public/worktask/detail";
        private readonly string upsertTaksUrl = "workspaces/api/public/worktask/upsert";

        public string Name => "WorkflowApplyRuleAgent";

        private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<ApplyRuleAgent>();

        public string Description => "Posso applicare delle regole ad un workflow";

        public ApplyRuleAgent(
            ChatClient chatClient,
            JarvisApiCaller jarvisApiCaller,
            IOptionsMonitor<JarvisConfig> jarvisOptions,
            ITemplateManager templateManager)
        {
            _chatClient = chatClient;
            _jarvisApiCaller = jarvisApiCaller;
            _jarvisOptions = jarvisOptions;
            _templateManager = templateManager;
        }

        /// <summary>
        /// Execute the agent and return a string containing the answer.l
        /// </summary>
        /// <returns></returns>
        public async Task<string> Execute(Dictionary<string, string> parameters, string prompt)
        {
            var context = parameters["context"];
            //extract workspace id in the form of WorkSpace_xxx with regex
            var workspaceId = Regex.Match(context, @"Workspace_\d+").Value;

            var url = getTaskUrl.Replace("{WORKSPACEID}", workspaceId);
            var dto = @"{""PageSize"" : 30,
  ""StartIndex"" : 0,
  ""FieldSearches"" : [] }";
            var result = await _jarvisApiCaller.CallApiAsync(url, dto);

            //parse dynamically with system.text.json
            var json = (JsonObject)JsonNode.Parse(result);
            var data = json["data"] as JsonArray;

            var wiList = new List<WorkItem>();
            await Task.WhenAll(data
                .Where(d => d != null)
                .OfType<JsonObject>()
                .Select(workitem => GetDetailOfWorkItem(wiList, workitem)));

            var itemDic = wiList.ToDictionary(wi => wi.Id);

            StringBuilder sb = new StringBuilder(1000);

            sb.AppendLine("[Regola]");
            foreach (var rule in wiList.Where(wi => wi.TaskTypeId == _jarvisOptions.CurrentValue.RuleTaskType))
            {
                //Append json serialized compact version of rule
                sb.AppendLine(Regex.Unescape(JsonSerializer.Serialize(rule, new JsonSerializerOptions() { WriteIndented = false })));
            }
            sb.AppendLine("[Task]");
            foreach (var wi in wiList.Where(wi => wi.TaskTypeId != _jarvisOptions.CurrentValue.RuleTaskType))
            {
                //Append json serialized compact version of rule
                sb.AppendLine(Regex.Unescape(JsonSerializer.Serialize(wi, new JsonSerializerOptions() { WriteIndented = false })));
            }

            var (systemMessage, llmPrompt) = _templateManager.GetGptCallTemplate("demo_workspace.txt", new Dictionary<string, string>()
            {
                ["data"] = sb.ToString(),
                ["currentdate"] = DateTime.Now.ToString("yyyy-MM-dd"),
            });

            var payload = CreateBasePayload(systemMessage, llmPrompt);
            var chatResult = await _chatClient.SendMessageAsync("gpt4", payload);

            //Handle situation where LLM returns text plus code snippet.

            //parse codeContent into array of WorkItemUpdate
            WorkItemUpdate[] workItemUpdates;
            if (chatResult.FunctionCall != null)
            {
                //we got a better function call result.
                var functionCallData = JsonSerializer.Deserialize<FunctionCallData>(chatResult.FunctionCall.Arguments)!;
                workItemUpdates = functionCallData.Data;
            }
            else
            {
                var responseRaw = chatResult.Content;
                if (responseRaw.Contains("'''"))
                {
                    var parsed = ChatResponseParser.ParseApiResponse(responseRaw);
                    responseRaw = parsed.First(p => p.IsCodeSnippet).Content;
                }

                try
                {
                    workItemUpdates = JsonSerializer.Deserialize<WorkItemUpdate[]>(responseRaw)!;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Cannot parse response:\n {prompt} {responseRaw}", llmPrompt, responseRaw);
                    return "Errore nella risposta di Jarvis.";
                }
            }
            List<long> updated = new List<long>();
            foreach (var workItemToUpdate in workItemUpdates)
            {
                if (String.IsNullOrEmpty(workItemToUpdate.Title))
                {
                    continue;
                }

                //we could have same title
                var original = itemDic[workItemToUpdate.Id];

                if (original.Title.Equals(workItemToUpdate.Title, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var upsertDto = $@"{{
""Id"" : ""{workItemToUpdate.Id}"",
""ChangeTitle"" :{{""Title"" : ""{workItemToUpdate.Title}""}}
}}";
                var upsertResult = await _jarvisApiCaller.CallApiAsync(upsertTaksUrl, upsertDto);
                var id = long.Parse(workItemToUpdate.Id.Split("_").Last());
                updated.Add(id);

                //update original dictionary item
                original.Title = workItemToUpdate.Title;
            }

            if (updated.Count == 0)
            {
                return "Non ho modificato nulla.";
            }

            //now we have all work item changed, we grab new title to verify what is changed.
            var sbResponse = new StringBuilder();
            foreach (var item in updated.Order())
            {
                var id = $"WorkTask_{item}";
                var newTitle = itemDic[id].Title;
                //                var detaildto = $@"{{
                //  ""WorkTaskId"" : ""WorkTask_{item}"",
                //  ""IncludeDescription"" : true,
                //  ""HistoricalVersionDto"" : {{
                //    ""Latest"" : true,
                //    }}
                //}}";
                //                var detail = await _jarvisApiCaller.CallApiAsync(getDetailUrl, detaildto);
                //                var ruleJson = (JsonObject)JsonNode.Parse(detail);
                //                var ruleData = ruleJson["data"] as JsonObject;
                //                var newTitle = Regex.Unescape(ruleData["title"].ToString());
                sbResponse.AppendLine($"[#{item}]({_jarvisOptions.CurrentValue.PublicBaseUrl.TrimEnd('/')}/UI/#/workspaces/{workspaceId}/WorkTask_{item}): {newTitle}\n");
            }

            return @$"### Ho modificato:
{sbResponse}";
        }

        private async Task GetDetailOfWorkItem(List<WorkItem> wiList, JsonObject workitem)
        {
            var id = workitem["id"].ToString();
            var title = Regex.Unescape(workitem["title"].ToString());
            var taskTypeId = workitem["taskTypeId"].ToString();
            var dueDate = workitem["dueDate"]?.ToString() ?? "";

            string content = "";
            //todo: remove N+1

            var detaildto = $@"{{
  ""WorkTaskId"" : ""{id}"",
  ""IncludeDescription"" : true

}}";
            try
            {
                var detail = await _jarvisApiCaller.CallApiAsync(getDetailUrl, detaildto);
                var ruleJson = (JsonObject)JsonNode.Parse(detail);
                var ruleData = ruleJson["data"] as JsonObject;
                content = Regex.Unescape(ruleData["description"].ToString());

                //extract html from content
                if (!string.IsNullOrEmpty(content))
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(content);
                    content = doc.DocumentNode.InnerText;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to get detail for {id}", id);
            }

            var wi = new WorkItem()
            {
                Id = id,
                Title = title,
                TaskTypeId = taskTypeId,
                DueDate = dueDate,
                Content = content,
            };
            wiList.Add(wi);
        }

        private static ApiPayload CreateBasePayload(
            string systemMessage,
            string chatQuestion)
        {
            var payload = new ApiPayload
            {
                Messages = new List<Message>
            {
                new Message { Role = "system", Content = systemMessage },
                new Message { Role = "user", Content = chatQuestion },
            },
                MaxTokens = 2000,
                Temperature = 0.2,
                FrequencyPenalty = 1,
                PresencePenalty = 2,
                TopP = 0.9,
                Stop = null
            };

            //now populate the function
            payload.Functions = new OpenAiFunctionDefinition[] {
                new OpenAiFunctionDefinition()
                {
                    Name = "update_task_title",
                    Description = "Update task title",
                    Parameters = new OpenAiFunctionParameters()
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenAiFunctionPropertyBase>()
                        {
                            ["data"] = new OpenAiFunctionArrayProperty()
                            {
                                Items = new OpenAiFunctionObjectProperty()
                                {
                                    Properties = new Dictionary<string, OpenAiFunctionPropertyBase>()
                                    {
                                        ["Id"] = new OpenAiFunctionProperty()
                                        {
                                            Type = "string",
                                            Description = "Id of the task to update",
                                        },
                                        ["Title"] = new OpenAiFunctionProperty()
                                        {
                                            Type = "string",
                                            Description = "New title of the task",
                                        },
                                        ["Explain"] = new OpenAiFunctionArrayProperty()
                                        {
                                            Items = new OpenAiFunctionProperty()
                                            {
                                                Type = "string",
                                                Description = "Explaination of the change",
                                            }
                                        },
                                    },
                                    Required = new string[] { "Id", "Title" },
                                }
                            },
                        },
                        Required = new string[] { "data" },
                    },
                }
            };

            //payload.FunctionsCall = "auto";
            payload.FunctionsCall = new CallSpecificFunction("update_task_title");
            return payload;
        }

        private class WorkItem
        {
            public required string Id { get; set; }

            public required string Title { get; set; }

            public required string TaskTypeId { get; set; }

            public required string Content { get; set; }

            public required string DueDate { get; set; }
        }

        private class WorkItemUpdate
        {
            [JsonPropertyName("Id")]
            public required string Id { get; set; }

            [JsonPropertyName("Title")]
            public string? Title { get; set; }

            [JsonPropertyName("Explain")]
            public string[] Explain { get; set; }
        }

        private class FunctionCallData 
        {
            [JsonPropertyName("data")] public WorkItemUpdate[] Data { get; set; } = null!;
        }
    }
}
