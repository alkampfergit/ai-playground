using AzureAiLibrary.CodeGeneration;
using System.Text.Json;

namespace AzureAiLibrary.Tests.CodeGenerators
{
    public class TopOfTheClassTests : IDisposable
    {
        private string _outDirectory;
        private TopOfTheClass _sut;

        public TopOfTheClassTests()
        {
            _outDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_outDirectory);
            _sut = new TopOfTheClass(_outDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(_outDirectory, true);
        }

        [Fact]
        public async Task GenerateChat1()
        {
            var testFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "Totc_1.json");
            var readJson = File.ReadAllText(testFile);
            var deserialized = JsonSerializer.Deserialize<SavedFile>(readJson)!;
            await _sut.GenerateAsync(deserialized.Messages);
        }

        [Fact]
        public async Task GenerateChat2()
        {
            var testFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "Totc_2.json");
            var readJson = File.ReadAllText(testFile);
            var deserialized = JsonSerializer.Deserialize<SavedFile>(readJson)!;
            await _sut.GenerateAsync(deserialized.Messages);
        }

        private class SavedFile
        {
            public MessageWithFragments[] Messages { get; set; } = null!;
        }
    }
}
