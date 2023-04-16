using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureAiLibrary.Tests.Helpers
{
    using AzureAiLibrary.Helpers;
    using System.IO;
    using System.Threading.Tasks;
    using Xunit;

    public class CommandExecutorTests
    {
        [Fact]
        public async Task TestExecuteAsync()
        {
            CommandExecutor executor = new CommandExecutor(Directory.GetCurrentDirectory());

            int exitCode = await executor.ExecuteAsync("dir");
            Assert.Equal(0, exitCode);

            exitCode = await executor.ExecuteAsync("cd ..");
            Assert.Equal(0, exitCode);

            exitCode = await executor.ExecuteAsync("dir");
            Assert.Equal(0, exitCode);

            await Assert.ThrowsAsync<ArgumentException>(async () => await executor.ExecuteAsync("cd non-existent-directory"));
        }
    }
}
