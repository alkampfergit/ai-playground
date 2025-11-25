#pragma warning disable OPENAI001

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenAI.Responses;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Spectre.Console;
using Azure.AI.OpenAI;
using OpenAI.Containers;
using System.ClientModel.Primitives;

class Program
{
    static async Task Main(string[] args)
    {
        DotEnv.Load();

        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_ENDPOINT");
        var azureApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var deploymentId = Environment.GetEnvironmentVariable("DEPLOYMENT_ID") ?? "gpt-5-nano";

        var openaiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY_NOT_AZURE");

        if (string.IsNullOrEmpty(azureEndpoint) || string.IsNullOrEmpty(azureApiKey))
        {
            AnsiConsole.MarkupLine("[red]Please set AZURE_ENDPOINT and OPENAI_API_KEY in the .env file.[/]");
            return;
        }

        var mode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose chat mode:")
                .AddChoices("Old Chat Model", "New Response Model", "Persistent Container"));

        if (mode == "Old Chat Model")
        {
            await OldChat(azureEndpoint, azureApiKey, deploymentId);
        }
        else if (mode == "New Response Model")
        {
            await NewResponse(azureEndpoint, azureApiKey, deploymentId);
        }
        else
        {
            await PersistentContainer(openaiApiKey);
        }
    }

    static async Task OldChat(string azureEndpoint, string azureApiKey, string deploymentId)
    {
        var client = new AzureOpenAIClient(
            new Uri(azureEndpoint),
            new ApiKeyCredential(azureApiKey));

        var chatClient = client.GetChatClient(deploymentId);

        var messages = new List<ChatMessage>();
        messages.Add(ChatMessage.CreateSystemMessage("You are a helpful assistant."));  ;
        AnsiConsole.Write(new Rule("[green]Old Chat Model[/]").RuleStyle("green dim"));
        AnsiConsole.MarkupLine("[dim]Type 'exit' to quit[/]\n");

        while (true)
        {
            var userInput = AnsiConsole.Ask<string>("[blue bold]You:[/] ");
            if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit")
                break;

            messages.Add(ChatMessage.CreateUserMessage(userInput));
            ChatCompletionOptions completionOptions = new ChatCompletionOptions();
           
            var completion = await chatClient.CompleteChatAsync(messages, completionOptions);

            var assistantMessage = completion.Value.Content[0].Text;
            messages.Add(ChatMessage.CreateAssistantMessage(assistantMessage));

            // Display assistant response in a panel
            var panel = new Panel(assistantMessage)
            {
                Header = new PanelHeader(" [yellow]Assistant[/] ", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow)
            };
            AnsiConsole.Write(panel);

            // Display token usage statistics
            var usage = completion.Value.Usage;
            var statsTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[dim]Metric[/]").Centered())
                .AddColumn(new TableColumn("[dim]Value[/]").Centered())
                .AddRow("[cyan]Input Tokens[/]", $"[white]{usage.InputTokenCount}[/]")
                .AddRow("[cyan]Output Tokens[/]", $"[white]{usage.OutputTokenCount}[/]")
                .AddRow("[cyan bold]Total Tokens[/]", $"[white bold]{usage.TotalTokenCount}[/]");

            // Add cached token information if available
            if (usage.InputTokenDetails != null)
            {
                var cachedCount = usage.InputTokenDetails.CachedTokenCount;
                statsTable.AddRow("[green]  └─ Cached Input[/]", $"[green]{cachedCount}[/]");
            }

            AnsiConsole.Write(statsTable);
            AnsiConsole.WriteLine();
        }
    }

    static async Task NewResponse(string azureEndpoint, string azureApiKey, string deploymentId)
    {
        // Specify API version using AzureOpenAIClientOptions.ServiceVersion enum
        // Use the latest version available in Azure.AI.OpenAI 2.1.0
        var clientOptions = new AzureOpenAIClientOptions(
            AzureOpenAIClientOptions.ServiceVersion.V2025_04_01_Preview);

        var client = new AzureOpenAIClient(
            new Uri(azureEndpoint),
            new ApiKeyCredential(azureApiKey),
            clientOptions);

        var responseClient = client.GetOpenAIResponseClient(deploymentId);

        string? conversationId = null;

        AnsiConsole.Write(new Rule("[green]New Response Model[/]").RuleStyle("green dim"));
        AnsiConsole.MarkupLine("[dim]Type 'exit' to quit[/]\n");

        while (true)
        {
            var userInput = AnsiConsole.Ask<string>("[blue bold]You:[/] ");
            if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit")
                break;

            var inputItems = new List<ResponseItem> { ResponseItem.CreateUserMessageItem(userInput) };

            var options = new ResponseCreationOptions
            {
                PreviousResponseId = conversationId,
                ReasoningOptions = new ResponseReasoningOptions()
                {
                    ReasoningEffortLevel = ResponseReasoningEffortLevel.Low,
                },
            };

            var jsonModel = (IJsonModel<ResponseCreationOptions>)options;

            // Force an additional member into the options properties bag.
            var newOptions = jsonModel.Create(BinaryData.FromObjectAsJson(new
            {
                text = new { verbosity = "low" },
            }),
            ModelReaderWriterOptions.Json);

            // Set the desired options with the strongly-typed interface.
            //options.Instructions = "Always talk like a literary scholar when you answer but be brief and make puns.";

            var result = await responseClient.CreateResponseAsync(inputItems, newOptions);

            OpenAIResponse response = result;

            conversationId = response.Id;

            foreach (var outputItem in response.OutputItems)
            {
                if (outputItem is ReasoningResponseItem reasoning)
                {
                    // Display reasoning summary
                    var summaryText = reasoning.GetSummaryText();
                    var reasoningContent = !string.IsNullOrWhiteSpace(summaryText)
                        ? summaryText
                        : $"Reasoning ID: {reasoning.Id}";

                    var reasoningPanel = new Panel(reasoningContent)
                    {
                        Header = new PanelHeader(" [aqua]Reasoning Process[/] ", Justify.Left),
                        Border = BoxBorder.Rounded,
                        BorderStyle = new Style(Color.Aqua)
                    };
                    AnsiConsole.Write(reasoningPanel);
                }
                else if (outputItem is MessageResponseItem message)
                {
                    // Display assistant response in a panel
                    var panel = new Panel(message.Content[0].Text)
                    {
                        Header = new PanelHeader(" [yellow]Assistant[/] ", Justify.Left),
                        Border = BoxBorder.Rounded,
                        BorderStyle = new Style(Color.Yellow)
                    };
                    AnsiConsole.Write(panel);
                }
            }

            // Display detailed token usage statistics
            var usage = response.Usage;
            var statsTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[dim]Metric[/]"))
                .AddColumn(new TableColumn("[dim]Value[/]").RightAligned())
                .AddRow("[cyan]Input Tokens[/]", $"[white]{usage.InputTokenCount}[/]")
                .AddRow("[cyan]Output Tokens[/]", $"[white]{usage.OutputTokenCount}[/]")
                .AddRow("[cyan bold]Total Tokens[/]", $"[white bold]{usage.TotalTokenCount}[/]");

            // Add input token details (cached tokens) - always display
            if (usage.InputTokenDetails != null)
            {
                var cachedCount = usage.InputTokenDetails.CachedTokenCount;
                var color = cachedCount > 0 ? "green" : "dim";
                statsTable.AddRow($"[{color}]  └─ Cached Input[/]", $"[{color}]{cachedCount}[/]");
            }

            // Add output token details (reasoning tokens)
            if (usage.OutputTokenDetails != null)
            {
                if (usage.OutputTokenDetails.ReasoningTokenCount > 0)
                {
                    statsTable.AddRow("[fuchsia]  └─ Reasoning Output[/]", $"[fuchsia]{usage.OutputTokenDetails.ReasoningTokenCount}[/]");
                }
            }

            AnsiConsole.Write(statsTable);
            AnsiConsole.WriteLine();
        }
    }

    static async Task PersistentContainer(string apiKey)
    {
        // Create a persistent container (lifetime-limited) that can hold state for tools like Code Interpreter
        ApiKeyCredential credentials = new(apiKey);
        var containersClient = new ContainerClient(credentials);

        var containerName = $"demo-container-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        var createBody = new CreateContainerBody(containerName)
        {
            ExpiresAfter = new CreateContainerBodyExpiresAfter(10) // expire in 10 minutes
        };

        var container = await containersClient.CreateContainerAsync(createBody);
        var containerId = container.Value.Id;

        AnsiConsole.Write(new Rule("[green]Persistent Container Mode[/]").RuleStyle("green dim"));
        AnsiConsole.MarkupLine($"[green]Container ID:[/] [dim]{containerId}[/]");
        AnsiConsole.MarkupLine("[dim]Type 'exit' to quit[/]\n");

        // Use the Responses client bound to your Azure deployment/model
        var clientOptions = new AzureOpenAIClientOptions(
            AzureOpenAIClientOptions.ServiceVersion.V2025_04_01_Preview);

        var openaIClient = new OpenAIClient(credentials);

        var responseClient = openaIClient.GetOpenAIResponseClient("gpt-5-nano");

        string? previousResponseId = null;

        while (true)
        {
            var userInput = AnsiConsole.Ask<string>("[blue bold]You:[/] ");
            if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit")
                break;

            var inputItems = new List<ResponseItem>
            {
                ResponseItem.CreateUserMessageItem(userInput)
            };

            // Attach a Code Interpreter tool bound to the persistent container.
            // This demonstrates how tool state and files can persist across requests via the container.
            var tool = new CodeInterpreterTool(new CodeInterpreterToolContainer(containerId));

            var optionsForResponse = new ResponseCreationOptions
            {
                Tools = { tool },
                PreviousResponseId = previousResponseId
            };

            var result = await responseClient.CreateResponseAsync(inputItems, optionsForResponse);

            OpenAIResponse response = result;
            previousResponseId = response.Id;

            foreach (var outputItem in response.OutputItems)
            {
                if (outputItem is ReasoningResponseItem reasoning)
                {
                    // Display reasoning summary
                    var summaryText = reasoning.GetSummaryText();
                    var reasoningContent = !string.IsNullOrWhiteSpace(summaryText)
                        ? summaryText
                        : $"Reasoning ID: {reasoning.Id}";

                    var reasoningPanel = new Panel(reasoningContent)
                    {
                        Header = new PanelHeader(" [aqua]Reasoning Process[/] ", Justify.Left),
                        Border = BoxBorder.Rounded,
                        BorderStyle = new Style(Color.Aqua)
                    };
                    AnsiConsole.Write(reasoningPanel);
                }
                else if (outputItem is MessageResponseItem message)
                {
                    // Display assistant response in a panel
                    var panel = new Panel(message.Content[0].Text)
                    {
                        Header = new PanelHeader(" [yellow]Assistant[/] ", Justify.Left),
                        Border = BoxBorder.Rounded,
                        BorderStyle = new Style(Color.Yellow)
                    };
                    AnsiConsole.Write(panel);
                }
                else if (outputItem is CodeInterpreterCallResponseItem ci)
                {
                    var codePanel = new Panel($"Status: {ci.Status}\nContainer: {ci.ContainerId}")
                    {
                        Header = new PanelHeader(" [fuchsia]Code Interpreter[/] ", Justify.Left),
                        Border = BoxBorder.Rounded,
                        BorderStyle = new Style(Color.Fuchsia)
                    };
                    AnsiConsole.Write(codePanel);
                }
            }

            // Display detailed token usage statistics
            var usage = response.Usage;
            var statsTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[dim]Metric[/]"))
                .AddColumn(new TableColumn("[dim]Value[/]").RightAligned())
                .AddRow("[cyan]Input Tokens[/]", $"[white]{usage.InputTokenCount}[/]")
                .AddRow("[cyan]Output Tokens[/]", $"[white]{usage.OutputTokenCount}[/]")
                .AddRow("[cyan bold]Total Tokens[/]", $"[white bold]{usage.TotalTokenCount}[/]");

            // Add input token details (cached tokens) - always display
            if (usage.InputTokenDetails != null)
            {
                var cachedCount = usage.InputTokenDetails.CachedTokenCount;
                var color = cachedCount > 0 ? "green" : "dim";
                statsTable.AddRow($"[{color}]  └─ Cached Input[/]", $"[{color}]{cachedCount}[/]");
            }

            // Add output token details (reasoning tokens)
            if (usage.OutputTokenDetails != null)
            {
                if (usage.OutputTokenDetails.ReasoningTokenCount > 0)
                {
                    statsTable.AddRow("[fuchsia]  └─ Reasoning Output[/]", $"[fuchsia]{usage.OutputTokenDetails.ReasoningTokenCount}[/]");
                }
            }

            AnsiConsole.Write(statsTable);
            AnsiConsole.WriteLine();
        }

        // Optional: clean up the container when finished
        try
        {
            await containersClient.DeleteContainerAsync(containerId);
            AnsiConsole.MarkupLine($"[green]Deleted container:[/] [dim]{containerId}[/]");
        }
        catch
        {
            // ignore errors during cleanup in sample
        }
    }
}

#pragma warning restore OPENAI001
