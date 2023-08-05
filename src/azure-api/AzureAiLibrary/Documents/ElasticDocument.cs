using Nest;
using System.Collections;

namespace AzureAiLibrary.Documents;

[ElasticsearchType(IdProperty = "Id")]
public class ElasticDocument : Dictionary<string, object>
{
    public string Id { get; set; } = null!;

    public string? Title
    {
        get
        {
            return GetStringProperty("title");
        }
        set { AddStringProperty("title", value ?? string.Empty); }
    }

    public ElasticDocument() : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    public ElasticDocument(string id) : this()
    {
        Id = id;
    }

    /// <summary>
    /// USed for metadata, id...
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public ElasticDocument AddStringProperty(string key, string value)
    {
        this[$"s_{key}"] = value;
        return this;
    }

    public string? GetStringProperty(string key)
    {
        return TryGetValue($"s_{key}", out var title) ? title as string : string.Empty;
    }

    /// <summary>
    /// Used for lots of text, something that needs to be analyzed and searched into.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="text"></param>
    /// <returns></returns>
    public ElasticDocument AddTextProperty(string key, string text)
    {
        this[$"t_{key}"] = text;
        return this;
    }

    public string? GetTextProperty(string key)
    {
        return TryGetValue($"t_{key}", out var title) ? title as string : string.Empty;
    }

    public SingleDenseVectorData GetVector(string fieldName)
    {
        //TODO: refactor because it is duplicate code
        var standardVectorProperty = $"v_{fieldName}_vector";
        var standardNormalizedVectorProperty = $"v_{fieldName}_normalized_vector";
        var gpt35VectorProperty = $"v_{fieldName}_gpt35_vector";
        var gpt35NormalizedVectorProperty = $"v_{fieldName}_gpt35_normalized_vector";

        return new SingleDenseVectorData(
            Id,
            fieldName,
            GetVectorData(standardVectorProperty),
            GetVectorData(standardNormalizedVectorProperty),
            GetVectorData(gpt35VectorProperty),
            GetVectorData(gpt35NormalizedVectorProperty));
    }

    private double[] GetVectorData(string standardVectorProperty)
    {
        return TryGetValue($"{standardVectorProperty}", out var rawVector) ? ((IEnumerable)rawVector).Cast<double>().ToArray() : Array.Empty<double>();
    }
}