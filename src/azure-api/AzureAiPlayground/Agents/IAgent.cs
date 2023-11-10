namespace AzureAiPlayground.Agents
{
    public interface IAgent
    {
        string Name { get; }

        string Description { get;  }

        Task<string> Execute(Dictionary<string, string> parameters, string prompt);
    }
}
