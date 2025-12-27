using PLang.Services.OutputStream.Transformers;
using System.Text;

namespace PLang.Services.OutputStream;

/// <summary>
/// Factory for creating transformers based on content type.
/// </summary>
public static class TransformerFactory
{
    public static ITransformer Create(string contentType, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        return contentType.ToLowerInvariant() switch
        {
            PlangContentTypes.Ndjson => new PlangTransformer(encoding),
            PlangContentTypes.Json => new JsonTransformer(encoding),
            PlangContentTypes.Text or "text/plain" => new TextTransformer(encoding),
            PlangContentTypes.Html or "text/html" => new HtmlTransformer(encoding),
            "application/json" => new JsonTransformer(encoding),
            "application/x-ndjson" => new PlangTransformer(encoding), // Legacy
            "application/plang+jsonl" => new PlangTransformer(encoding), // Legacy
            _ => new TextTransformer(encoding)
        };
    }
}
