namespace AzureAiLibrary.tests
{
    using AzureAiLibrary.Helpers;
    using System;
    using System.IO;
    using Xunit;

    public class FolderDatabaseTests : IDisposable
    {
        private FolderDatabase<SampleTestClass> _database;
        private string _testFolder;

        public FolderDatabaseTests()
        {
            _testFolder = Path.Combine(Path.GetTempPath(), "FolderDatabaseTests");
            _database = new FolderDatabase<SampleTestClass>(_testFolder);
        }

        public void Dispose()
        {
            Directory.Delete(_testFolder, true);
        }

        [Fact]
        public void TestSaveAndLoad()
        {
            var sampleObject = new SampleTestClass { Id = 1, Name = "Test Object 1" };
            _database.Save("1", "Test Object 1", sampleObject);

            var loadedObject = _database.Load("1");
            Assert.Equal(sampleObject.Id, loadedObject.Record.Id);
            Assert.Equal(sampleObject.Name, loadedObject.Record.Name);
        }

        [Fact]
        public void TestSearch()
        {
            var sampleObject1 = new SampleTestClass { Id = 1, Name = "Test Object 1" };
            _database.Save("1", "Test Object 1", sampleObject1);
            var sampleObject2 = new SampleTestClass { Id = 2, Name = "Test Object 2" };
            _database.Save("2", "Test Object 2", sampleObject2);

            var results = _database.Search("object 2");
            Assert.Single(results);
            Assert.Equal("2", results[0].Id);
            Assert.Equal("Test Object 2", results[0].Description);
        }

        [Fact]
        public void TestList()
        {
            for (int i = 1; i <= 25; i++)
            {
                var sampleObject = new SampleTestClass { Id = i, Name = $"Test Object {i}" };
                _database.Save(i.ToString(), $"Test Object {i}", sampleObject);
            }

            var list = _database.List().Select(r => r.Record).ToList();
            Assert.Equal(20, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                Assert.Equal(25 - i, list[i].Id);
                Assert.Equal($"Test Object {25 - i}", list[i].Name);
            }
        }
    }

    public class SampleTestClass
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
