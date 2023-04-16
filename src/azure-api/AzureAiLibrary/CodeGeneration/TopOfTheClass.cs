using AzureAiLibrary.Helpers;

namespace AzureAiLibrary.CodeGeneration
{
    public class TopOfTheClass
    {
        private readonly string _baseDirectory;
        private readonly CommandExecutor _commandExecutor;

        public TopOfTheClass(string baseDirectory)
        {
            _baseDirectory = baseDirectory;
            _commandExecutor = new CommandExecutor(baseDirectory);
        }

        /// <summary>
        /// Generate a program using the top of the class programmer prompt respnose
        /// </summary>
        /// <param name="messages"></param>
        /// <returns></returns>
        public async Task GenerateAsync(IEnumerable<MessageWithFragments> messages)
        {
            //we have some fixed structure, we need to find the first response to the
            //initialize command
            var startOfInstruction = messages
                .SkipWhile(m => !m.Message.Content.StartsWith("initialize"))
                .Skip(1);

            var initCommand = startOfInstruction.First();
            foreach (var code in initCommand.Fragments.Where(f => f.IsCodeSnippet))
            {
                var lines = code.Content.Split('\n');
                foreach (var line in lines.Where(l => !String.IsNullOrEmpty(l)))
                {
                    await _commandExecutor.ExecuteAsync(line);
                }
            }

            //ok now we need to get all files 
            var files = startOfInstruction.Skip(2)
                .Where(m => m.Message.Role == "assistant");
            var currentDir = _commandExecutor.CurrentDirectory;
            foreach (var message in files)
            {
                //each file starts with a file name
                var firstLine = message.Fragments.First().Content;
                string file;
                if (firstLine.StartsWith('.'))
                {
                    file = Path.Combine(_baseDirectory, firstLine.Substring(2));
                }
                else
                {
                    var fileNameStart = firstLine.IndexOf(' ');
                    file = Path.Combine(currentDir, firstLine.Substring(fileNameStart + 1));
                }
                var fileContent = message.Fragments.FirstOrDefault(f => f.IsCodeSnippet);

                if (fileContent != null)
                {
                    var path = Path.GetDirectoryName(file)!;
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                    await File.WriteAllTextAsync(file, fileContent.Content);
                }
            }
        }
    }
}
