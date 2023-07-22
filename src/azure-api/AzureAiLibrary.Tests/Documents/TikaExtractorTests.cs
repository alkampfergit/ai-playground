using AzureAiLibrary.Documents;
using AzureAiLibrary.Tests.TestFiles;

namespace AzureAiLibrary.Tests.Documents
{
    public class TikaExtractorTests
    {
        private TikaOutOfProcess _sut;

        public TikaExtractorTests()
        {
            _sut = new TikaOutOfProcess(
                Path.Combine(Environment.GetEnvironmentVariable("JAVA_HOME")!, "bin", "java.exe"),
                Environment.GetEnvironmentVariable("TIKA_HOME")!);
        }

        [Fact]
        public async Task Is_able_to_extract()
        {
            var extracted = await _sut.GetHtmlContentAsync(TestFilesHelper.SamplePdf);
            Assert.NotNull(extracted);


        }
    }
}
