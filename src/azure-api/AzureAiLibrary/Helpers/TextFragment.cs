using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureAiLibrary.Helpers
{
    public class TextFragment
    {
        public string Content { get; init; } = null!;
        public bool IsCodeSnippet { get; init; }
        public string Language { get; init; } = null!;
    }
}
