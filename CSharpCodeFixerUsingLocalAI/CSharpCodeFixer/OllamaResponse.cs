using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CSharpCodeFixer
{
    public record OllamaResponse
    {
        [property: JsonPropertyName("response")]
        public string? Response { get; set; }
    }
}
