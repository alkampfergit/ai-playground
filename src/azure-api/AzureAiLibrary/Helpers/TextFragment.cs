using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureAiLibrary.Helpers
{
    public class TextFragment
    {
        public string Content { get; set; }
        public bool IsCodeSnippet { get; set; }
        public string Language { get; set; }
    }
}
