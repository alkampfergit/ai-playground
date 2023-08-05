using MongoDB.Bson;

namespace AzureAiLibrary.Documents
{
    public class MongoEmbedding
    {
        public ObjectId Id { get; set; }

        public string DocumentId { get; set; }

        public int PageNumber { get; set; }

        public List<double> Vector { get; set; }

        public List<double> VectorGpt35 { get; set; }

        public List<double> VectorNormalized { get; set; }

        public List<double> VectorGpt35Normalized { get; set; }

        public string Model { get; set; }

        public DateTime CreatedOn { get; set; }
    }
}
