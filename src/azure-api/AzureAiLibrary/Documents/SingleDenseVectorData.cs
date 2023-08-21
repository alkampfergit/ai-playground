namespace AzureAiLibrary.Documents;

public record SingleDenseVectorData(
    string Id,
    string FieldName,
    double[] VectorData,
    double[]? NormalizedVectorData,
    double[]? Gpt35VectorData,
    double[]? Gpt35NormalizedVectorData
);
