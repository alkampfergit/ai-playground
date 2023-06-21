using System.Text.Json.Serialization;
using Azure.AI.OpenAI;

namespace AzureAiLibrary;

public class Message
{
    private readonly StreamingChatCompletions _streamingChatCompletions;

    public Message(string role, string content)
    {
        Role = role;
        Content = content;
    }

    [JsonConstructor]
    public Message()
    {
    }

    public Message(StreamingChatCompletions streamingChatCompletions)
    {
        _streamingChatCompletions = streamingChatCompletions;
        Content = "";
        Role = "assistant";
        Task.Run(ReadStreamResponse);
    }

    private async Task ReadStreamResponse()
    {
        try
        {
            using (_streamingChatCompletions)
            {
                var streamingChoices = _streamingChatCompletions.GetChoicesStreaming(CancellationToken.None);

                await foreach (StreamingChatChoice choice in streamingChoices)
                {
                    var messageEnumerable = choice.GetMessageStreaming();
                    await foreach (ChatMessage message in messageEnumerable)
                    {
                        //update content and signal that the content changed.
                        Content += message.Content;
                        OnContentChanged();
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public static Message CreateSystemMessage(string message)
    {
        return new Message("system", message);
    }

    public static Message CreateAssistantMessage(string message)
    {
        return new Message("assistant", message);
    }

    public static Message CreateUserMessage(string message)
    {
        return new Message("user", message);
    }

    public event EventHandler ContentChanged;

    public void OnContentChanged()
    {
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    [JsonPropertyName("role")] public string Role { get; set; }

    [JsonPropertyName("content")] public string Content { get; set; }

    public ChatRole GetChatRole()
    {
        if ("system".Equals(Role, StringComparison.OrdinalIgnoreCase))
        {
            return ChatRole.System;
        }
        else if ("assistant".Equals(Role, StringComparison.OrdinalIgnoreCase))
        {
            return ChatRole.Assistant;
        }
        else if ("user".Equals(Role, StringComparison.OrdinalIgnoreCase))
        {
            return ChatRole.User;
        }
        else
        {
            throw new System.Exception("Unknown role: " + Role);
        }
    }
}