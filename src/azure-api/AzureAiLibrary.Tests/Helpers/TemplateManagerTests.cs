namespace AzureAiLibrary.Tests.Helpers
{
    using AzureAiLibrary.Configuration;
    using AzureAiLibrary.Helpers;
    using Microsoft.Extensions.Options;
    using Moq;
    using Xunit;

    public class TemplateHelperTests
    {
        [Theory()]
        [InlineData("@test")]
        [InlineData("@TEst")]
        public void ReplaceWordsStartingWithAt_ReplacesTokensWithTemplateContent(string template)
        {
            // Arrange
            var mockTemplateManager = new Mock<ITemplateManager>();
            var templateTrimmed = template.TrimStart('@');
            mockTemplateManager.Setup(m => m.GetTemplateContent(templateTrimmed)).Returns("This is a test template.");
            var helper = new TemplateHelper(mockTemplateManager.Object);

            string input = $"Hello {template}, how are you?";
            const string expected = "Hello This is a test template., how are you?";

            // Act
            string result = helper.ExpandTemplates(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Resilient_to_null()
        {
            // Arrange
            var mockTemplateManager = new Mock<ITemplateManager>();
            var helper = new TemplateHelper(mockTemplateManager.Object);

            const string? input = null;
            const string expected = "";

            // Act
            string result = helper.ExpandTemplates(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetTemplateContent_ReturnsTemplateNameWhenManagerReturnsNull()
        {
            // Arrange
            var mockTemplateManager = new Mock<ITemplateManager>();
            mockTemplateManager.Setup(m => m.GetTemplateContent("test")).Returns((string)null);
            var helper = new TemplateHelper(mockTemplateManager.Object);

            const string templateName = "I have a missing @test template";
            const string expected = "I have a missing @test template";

            // Act
            string result = helper.ExpandTemplates(templateName);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("This is the system message.", "This is the prompt withtout substitution\r\nin multiline")]
        [InlineData("This is the system message\r\nwithnewline.", "This is the prompt withtout substitution\r\nin multiline")]
        public void Basic_gpt_without_substitution(string system, string prompt)
        {
            // Arrange
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);

            try
            {
                var templateName = Path.Combine(dir, "test.txt");
                File.WriteAllText(templateName, $"{system}\n\n{prompt}");
                var chatConfig = new ChatConfig
                {
                    TemplateDir = dir
                };
                var option = Mock.Of<IOptionsMonitor<ChatConfig>>(_ => _.CurrentValue == chatConfig);
                var sut = new DefaultTemplateManager(option);

                // Act
                var (tsystem, tprompt) = sut.GetGptCallTemplate("test", new Dictionary<string, string> ());  

                // Assert
                Assert.Equal(system, tsystem);
                Assert.Equal(prompt, tprompt);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Basic_gpt_with_substitution()
        {
            // Arrange
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);

            try
            {
                var templateName = Path.Combine(dir, "test.txt");
                File.WriteAllText(templateName, $"This is the prompt with {{template1}} content\n\nThis is the prompt with {{content}} message");
                var chatConfig = new ChatConfig
                {
                    TemplateDir = dir
                };
                var option = Mock.Of<IOptionsMonitor<ChatConfig>>(_ => _.CurrentValue == chatConfig);
                var sut = new DefaultTemplateManager(option);

                // Act
                var (tsystem, tprompt) = sut.GetGptCallTemplate("test", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) 
                {
                    ["TEMplate1"] = "aaaaa",
                    ["conteNT"] = "bbbbb"
                });

                // Assert
                Assert.Equal("This is the prompt with aaaaa content", tsystem);
                Assert.Equal("This is the prompt with bbbbb message", tprompt);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
