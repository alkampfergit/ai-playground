namespace AzureAiLibrary.Documents;

public record SingleDenseVectorData(
    string id,
    string fieldName,
    double[] vectorData,
    double[] normalizedVectorData,
    double[] gpt35VectorData,
    double[] gpt35NormalizedVectorData);
