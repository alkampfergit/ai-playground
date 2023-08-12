using MongoDB.Bson;

namespace AzureAiLibrary.Documents
{
    public class MongoEmbedding
    {
        public ObjectId Id { get; set; }

        public string DocumentId { get; set; } = null!;

        public int PageNumber { get; set; }

        public List<double> Vector { get; set; } = null!;

        public List<double> VectorGpt35 { get; set; } = null!;

        public List<double> VectorNormalized { get; set; } = null!;

        public List<double> VectorGpt35Normalized { get; set; } = null!;

        public string Model { get; set; } = null!;

        public DateTime CreatedOn { get; set; }
    }
}
