using PLang.Exceptions;
using PLang.Services.CompilerService;

namespace PLang.Utils.Extractors;

public interface IContentExtractor
{
    public string LlmResponseType { get; set; }
    public object Extract(string content, Type responseType);
    public T Extract<T>(string content);
    string GetRequiredResponse(Type scheme);
}

public class TextExtractor : IContentExtractor
{
    public string LlmResponseType
    {
        get => "text";
        set { }
    }

    public object Extract(string content, Type responseType)
    {
        return content;
    }

    public T Extract<T>(string content)
    {
        return (T)Extract(content, typeof(T));
    }

    public string GetRequiredResponse(Type scheme)
    {
        return "";
    }
}

public class GenericExtractor : IContentExtractor
{
    private string? type;

    public GenericExtractor(string? type)
    {
        this.type = type;
    }

    public string LlmResponseType
    {
        get => type;
        set => type = value;
    }

    public T Extract<T>(string content)
    {
        return (T)Extract(content, typeof(T));
    }

    public object Extract(string content, Type responseType)
    {
        return ExtractByType(content, type);
    }

    public string GetRequiredResponse(Type scheme)
    {
        if (!string.IsNullOrEmpty(type))
            return "Do NOT write summary, no extra text to explain, be concise. Only write the raw response in ```" +
                   type;
        return "";
    }

    public object ExtractByType(string content, string? contentType = null)
    {
        if (contentType == null && content.StartsWith("```"))
            contentType = content.Substring(0, content.IndexOf("\n")).Replace("```", "").Trim();
        if (contentType == null) return content;

        var idx = content.IndexOf($"```{contentType}");
        if (idx != -1)
        {
            var newContent = content.Substring(idx + $"```{contentType}".Length);
            newContent = newContent.Substring(0, newContent.LastIndexOf("```"));
            return newContent;
        }

        return content;
    }
}

public class CSharpExtractor : IContentExtractor
{
    public string LlmResponseType
    {
        get => "csharp";
        set { }
    }

    public object Extract(string content, Type responseType)
    {
        var htmlExtractor = new HtmlExtractor();
        var implementation = htmlExtractor.ExtractByType(content, "csharp") as string;
        var json = htmlExtractor.ExtractByType(content, "json");

        var jsonExtractor = new JsonExtractor();
        var jsonObject = jsonExtractor.Extract(json.ToString()!, responseType);

        if (implementation != null && implementation.Contains("System.IO."))
            implementation = implementation.Replace("System.IO.", "PLang.SafeFileSystem.");

        if (responseType == typeof(CodeImplementationResponse))
        {
            var cir = jsonObject as CodeImplementationResponse;
            var ci = new CodeImplementationResponse(cir.Namespace, cir.Name, implementation, cir.InputParameters,
                cir.OutParameters, cir.Using, cir.Assemblies);

            return ci;
        }
        else
        {
            var cir = jsonObject as ConditionImplementationResponse;
            var ci = new ConditionImplementationResponse(cir.Namespace, cir.Name, implementation, cir.InputParameters,
                cir.Using, cir.Assemblies, cir.GoalToCallOnTrue, cir.GoalToCallOnFalse, cir.GoalToCallOnTrueParameters,
                cir.GoalToCallOnFalseParameters);

            return ci;
        }

        throw new BuilderException($"Response type '{responseType}' is not valid");
    }


    public T Extract<T>(string content)
    {
        return (T)Extract(content, typeof(T));
    }

    public string GetRequiredResponse(Type scheme)
    {
        return @$"Only write the raw c# code and json scheme, no summary, no extra text to explain, be concise.
	YOU MUST implement all code needed and valid c# code. 
	You must return ```csharp for the code implementation and ```json scheme: {TypeHelper.GetJsonSchema(scheme)}";
    }
}