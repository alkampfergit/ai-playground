using HtmlAgilityPack;
using Serilog;

namespace VectorizationSample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            Console.WriteLine("Hello, World!");

            TikaAnalyzer analyzer = new TikaAnalyzer(
                @"C:\Program Files\Amazon Corretto\jdk18.0.2_9\bin\java.exe",
                Environment.GetEnvironmentVariable("TIKA_HOME"));

            var extracted = analyzer.GetHtmlContent(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "sample.pdf"));

            //Parse HTML with agility pack
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(extracted);
            var pages = htmlDoc.DocumentNode.SelectNodes("//div[@class='page']");
            var allPages = pages.ToList();
            Console.WriteLine("We have {0} pages", allPages.Count);
            foreach (var page in allPages)
            {
                var pageText = page.InnerText;
                Console.WriteLine(pageText);
            }

            Console.ReadKey();
        }
    }
}