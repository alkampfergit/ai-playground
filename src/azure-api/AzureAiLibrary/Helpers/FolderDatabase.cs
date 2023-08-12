using System.Text.Json;

namespace AzureAiLibrary.Helpers
{
    public class FolderDatabase<T> where T : class
    {
        private readonly string _databaseFolder;

        public FolderDatabase(string databaseFolder)
        {
            _databaseFolder = databaseFolder;
            if (!Directory.Exists(_databaseFolder))
            {
                Directory.CreateDirectory(_databaseFolder);
            }
        }

        public void Save(string id, string description, T chatUi)
        {
            var filePath = Path.Combine(_databaseFolder, id + ".json");
            var chatUiEntry = new DatabaseEntry
            {
                Id = id,
                Description = description,
                Record = chatUi,
                LastModified = DateTime.UtcNow
            };
            File.WriteAllText(filePath, JsonSerializer.Serialize(chatUiEntry));
        }

        public void Delete(string id)
        {
            var filePath = Path.Combine(_databaseFolder, id + ".json");
            if (File.Exists(filePath)) File.Delete(filePath);
        }

        public DatabaseEntry? Load(string id)
        {
            var filePath = Path.Combine(_databaseFolder, id + ".json");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"ChatUi with id {id} not found.");
            }

            var chatUiEntry = JsonSerializer.Deserialize<DatabaseEntry>(File.ReadAllText(filePath));
            return chatUiEntry;
        }

        public List<DatabaseEntry> Search(string searchText)
        {
            return Directory.GetFiles(_databaseFolder, "*.json")
                .Select(file => JsonSerializer.Deserialize<DatabaseEntry>(File.ReadAllText(file)))
                .Where(entry => entry != null && entry.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList()!;
        }

        public List<DatabaseEntry> List()
        {
            return Directory.GetFiles(_databaseFolder, "*.json")
                .Select(file => JsonSerializer.Deserialize<DatabaseEntry>(File.ReadAllText(file)))
                .Where(entry => entry != null)
                .OrderByDescending(entry => entry!.LastModified)
                .Take(20)
                .ToList()!;
        }

        public class DatabaseEntry
        {
            public string Id { get; init; } = null!;
            public string Description { get; init; }  = null!;
            public T Record { get; init; } = null!;
            public DateTime LastModified { get; set; }
        }
    }
}
