namespace CSharpPythonWrapper;
using System.Diagnostics;

public class PythonWrapper
{
    public string Execute(string scriptPath)
    {
        string python3location = "/workspaces/ai-playground/src/python/deeplearningai/deep/bin/python3";

        ProcessStartInfo start = new ProcessStartInfo();
        start.FileName = python3location;
        start.Arguments = scriptPath;
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;

        using Process process = Process.Start(start);

        using System.IO.StreamReader reader = process.StandardOutput;

        string result = reader.ReadToEnd();
        process.WaitForExit();
        return result;
    }
}
