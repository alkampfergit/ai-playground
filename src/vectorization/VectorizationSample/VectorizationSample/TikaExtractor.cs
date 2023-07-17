using Serilog;
using System.Diagnostics;

namespace VectorizationSample
{
    public class TikaAnalyzer
    {
        private string? _tikaLocation;
        private string _pathToJavaExe;

        public TikaAnalyzer(string javaExePath, string tikaJarPath)
        {
            _tikaLocation = tikaJarPath;
            _pathToJavaExe = javaExePath;
            if (!File.Exists(_tikaLocation))
            {
                throw new Exception(string.Format("Tika not found on {0}", _tikaLocation));
            }
            if (!File.Exists(_pathToJavaExe))
            {
                throw new Exception(string.Format("Java not found on {0}", _pathToJavaExe));
            }
        }

        public string GetHtmlContent(string pathToInputFile)
        {
            try
            {
                var arguments = String.Format("-jar {0} -h \"{1}\"", _tikaLocation, pathToInputFile);

                Log.Debug("Executing {0} {1}", _pathToJavaExe, arguments);

                var psi = new ProcessStartInfo(_pathToJavaExe, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                string content = null;
                using (var p = Process.Start(psi))
                {
                    //wait for 30 seconds with timeout, if extraction requires more than 30 seconds file is really too big or problematic
                    var closedCorrectly = p.WaitForExit(1000 * 30);
                    if (!closedCorrectly)
                    {
                        //we have a timeout, ok we really need to kill the process and consider impossible to extract text from this file
                        p.Kill();
                        return "";
                    }

                    using (var reader = p.StandardOutput)
                    {
                        content = reader.ReadToEnd();
                    }

                    if (p.ExitCode == 0)
                    {
                        return content;
                    }
                    else
                    {
                        Log.Error("failed extracting with {0} exit code {1}", _tikaLocation, p.ExitCode);
                    }
                }
            }
            catch (Exception)
            {
                Log.Error("Error extracting with tika {0}", _tikaLocation);
            }

            return "";
        }
    }
}
