using System;
using System.IO;

public static class DotEnv
{
    public static void Load(string fileName = ".env")
    {
        string currentDir = Directory.GetCurrentDirectory();
        string? dir = currentDir;

        while (dir != null)
        {
            string filePath = Path.Combine(dir, fileName);
            if (File.Exists(filePath))
            {
                foreach (var line in File.ReadAllLines(filePath))
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                    }
                }
                break; // Load the first .env found
            }
            dir = Path.GetDirectoryName(dir);
        }
    }
}