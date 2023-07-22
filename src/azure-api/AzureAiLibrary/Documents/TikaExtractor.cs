using HtmlAgilityPack;
using MongoDB.Driver;
using Serilog;
using System.Diagnostics;
using System.Text;

namespace AzureAiLibrary.Documents
{
    public class TikaExtractor
    {
        private TikaOutOfProcess _tikaOutOfProcess;
        private IMongoCollection<MongoRawDocument> _documents;

        private ILogger Logger = Log.ForContext<TikaExtractor>();

        public TikaExtractor(
            TikaOutOfProcess tikaOutOfProcess,
            IMongoDatabase db)
        {
            _tikaOutOfProcess = tikaOutOfProcess;
            _documents = db.GetCollection<MongoRawDocument>("raw_documents");

            //Fire and forget
            _documents.Indexes.CreateOne(
                new CreateIndexModel<MongoRawDocument>(
                    Builders<MongoRawDocument>.IndexKeys.Ascending(x => x.Analyze),
                    new CreateIndexOptions {
                        Sparse = true,
                        Background = true,
                        Name = "Analyze"
                    }));
        }

        /// <summary>
        /// Syncronously extract data from a directory.
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="fileFilter"></param>
        /// <returns></returns>
        public async Task Extract(string directory, string fileFilter)
        {
            //Iterate on all files in directory and subdirectories matching file filter and the
            //extract with tika, protect with try catch and save a MongoRawDocument
            //with the extracted data
            var files = Directory.GetFiles(directory, fileFilter, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    Logger.Information("Extracting text with tika from {0}", file);
                    var extracted = await _tikaOutOfProcess.GetHtmlContentAsync(file);
                    if (extracted != null)
                    {
                        var rawDocument = new MongoRawDocument
                        {
                            Id = file,
                            Pages = extracted.Pages,
                            Metadata = extracted.Metadata,
                            Analyze = DateTime.UtcNow
                        };

                        await _documents.ReplaceOneAsync(
                            x => x.Id == rawDocument.Id,
                            rawDocument,
                            new ReplaceOptions { IsUpsert = true });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error extracting {0}", file);
                }
            }
        }
    }

    public class TikaOutOfProcess
    {
        private string? _tikaLocation;
        private string _pathToJavaExe;

        private ILogger Logger = Log.ForContext<TikaExtractor>();

        public TikaOutOfProcess(string javaExePath, string tikaJarPath)
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

        public async Task<TikaExtractedData?> GetHtmlContentAsync(string pathToInputFile)
        {
            try
            {
                var arguments = String.Format("-jar {0} -h \"{1}\"", _tikaLocation, pathToInputFile);

                Logger.Debug("Executing {0} {1}", _pathToJavaExe, arguments);

                var psi = new ProcessStartInfo(_pathToJavaExe, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardError = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                string? content;
                Logger.Information("About to extract tika content from {0} with commandline \"{1}\" {2}", pathToInputFile, _tikaLocation, arguments);
                using (var p = Process.Start(psi))
                {
                    using var reader = p.StandardOutput;
                    var cancellationToken = new CancellationTokenSource(10_000);

                    //fire and forget read to end async
                    //TODO: read error in another thread, so we can log it.
                    StringBuilder sb = new StringBuilder();
                    string? line;
                    while ((line = await reader.ReadLineAsync(cancellationToken.Token)) != null) 
                    {
                        sb.AppendLine(line);
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        //we have a timeout, ok we really need to kill the process and consider impossible to extract text from this file
                        Logger.Error("Tika timeout reached, we need to kill the process");
                        p.Kill();
                        return null;
                    }

                    //need to check if the exit code is ok.
                    if (p.ExitCode == 0)
                    {
                        return CreateExtractedData(sb.ToString());
                    }
                    else
                    {
                        Logger.Error("failed extracting with {0} exit code {1}", _tikaLocation, p.ExitCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error extracting with tika {0}", ex.Message);
            }

            return null;
        }

        private TikaExtractedData CreateExtractedData(string content)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(content);
            var pages = htmlDoc.DocumentNode.SelectNodes("//div[@class='page']");
            var allPages = pages.Select(p => p.InnerHtml).ToList();
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> metadata = new Dictionary<string, IReadOnlyCollection<string>>();
            var metaNodes = htmlDoc.DocumentNode.SelectNodes("//meta");
            if (metaNodes != null)
            {
                metadata = metaNodes.Select(n => new
                {
                    Name = n.GetAttributeValue("name", null),
                    Content = n.GetAttributeValue("content", null)
                })
                    .GroupBy(n => n.Name)
                    .ToDictionary(n => n.Key, n => (IReadOnlyCollection<string>)n.Select(x => x.Content).ToArray());
            }
            return new TikaExtractedData(metadata, allPages);
        }
    }

    public record TikaExtractedData(IReadOnlyDictionary<string, IReadOnlyCollection<string>> Metadata, IReadOnlyCollection<string> Pages);
}
