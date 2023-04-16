namespace AzureAiLibrary.Helpers
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    public class CommandExecutor
    {
        private string _currentDirectory;

        public CommandExecutor(string startingDirectory)
        {
            _currentDirectory = startingDirectory;
        }

        public string CurrentDirectory => _currentDirectory;

        public async Task<int> ExecuteAsync(string commandLine)
        {
            commandLine = commandLine.Trim('\n', '\r');
            string[] commandArgs = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (string.Equals(commandArgs[0], "cd", StringComparison.OrdinalIgnoreCase))
            {
                if (commandArgs.Length < 2)
                {
                    throw new ArgumentException("cd command requires a directory argument");
                }

                string newDirectory = commandArgs[1];
                if (!Path.IsPathRooted(newDirectory))
                {
                    newDirectory = Path.Combine(_currentDirectory, newDirectory);
                }

                if (!Directory.Exists(newDirectory))
                {
                    return 0;
                }

                _currentDirectory = newDirectory;
                return 0;
            }
            else
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = $"/c {commandLine}";
                startInfo.WorkingDirectory = _currentDirectory;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
                    process.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync();
                    return process.ExitCode;
                }
            }
        }
    }

}
