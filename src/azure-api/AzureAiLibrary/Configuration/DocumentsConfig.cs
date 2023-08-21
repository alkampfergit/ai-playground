namespace AzureAiLibrary.Configuration
{
    public class DocumentsConfig
    {
        public string MongoUrl { get; set; } = null!;

        public string ElasticUrl { get; set; } = null!;
        public string Tika { get; set; } = null!;
        public string JavaBin { get; set; } = null!;

        /// <summary>
        /// To tokenize bert or other models we simply call a flask endpoint created in python
        /// that can be found on the very same repository at this path
        /// src\python\langchainVarious\vectorization\vectorservice.py
        /// </summary>
        public string PythonTokenizerFlaskUrl { get; set; } = null!;
    }
}
