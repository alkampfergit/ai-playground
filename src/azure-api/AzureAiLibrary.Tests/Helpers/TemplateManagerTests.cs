namespace AzureAiLibrary.Tests.Helpers
{
    using AzureAiLibrary.Helpers;
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
    }
}
