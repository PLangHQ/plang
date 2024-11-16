using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using PLang.Exceptions;

namespace PLang.Utils.Extractors;

public class JsonExtractor : GenericExtractor, IContentExtractor
{
    public JsonExtractor() : base("json")
    {
    }

    public new T Extract<T>(string content)
    {
        return (T)Extract(content, typeof(T));
    }

    public new object? Extract(string? content, Type responseType)
    {
        if (content == null) return null;
        if (responseType == typeof(string)) return content;

        try
        {
            try
            {
                if (content.Trim().Contains("```" + LlmResponseType))
                    content = ExtractByType(content, "json").ToString()!;
                return JsonConvert.DeserializeObject(content, responseType) ?? "";
            }
            catch (Exception ex)
            {
                var newContent = FixMalformedJson(content);
                var obj = JsonConvert.DeserializeObject(newContent, responseType);
                if (obj != null) return obj;

                throw new ParsingException($"Error parsing content to json. Content:\n\n{content}", ex);
            }
        }
        catch
        {
            try
            {
                // Use a regular expression to match JSON-like objects
                var regex = new Regex(@"\{(?:[^{}]|(?<Level>\{)|(?<-Level>\}))+\}",
                    RegexOptions.Multiline | RegexOptions.Compiled);
                //Regex regex = new Regex(@"(\{.*?\}|\[.*?\])", RegexOptions.Singleline | RegexOptions.Compiled);
                var newContent = FixMalformedJson(content);
                var matches = regex.Matches(newContent);
                if (responseType.IsArray)
                {
                    var sb = new StringBuilder("[");
                    foreach (Match match in matches)
                        if (match.Success)
                        {
                            if (sb.Length > 1) sb.Append(",");
                            sb.Append(match.Value);
                        }

                    sb.Append("]");
                    return JsonConvert.DeserializeObject(sb.ToString(), responseType) ?? "";
                }

                foreach (Match match in matches)
                    if (match.Success)
                        try
                        {
                            return JsonConvert.DeserializeObject(match.Value, responseType) ?? "";
                        }
                        catch (Exception ex)
                        {
                            throw new ParsingException($"Error parsing content to json. Content:\n\n{content}", ex);
                        }

                throw new ParsingException($"Error parsing content to json. Content:\n\n{content}",
                    new Exception("Error parsing content to json"));
            }
            catch (Exception ex2)
            {
                throw new ParsingException($"Error parsing content to json. Content:\n\n{content}", ex2);
            }
        }
    }

    public new string GetRequiredResponse(Type type)
    {
        var strScheme = TypeHelper.GetJsonSchema(type);
        return GetRequiredResponse(strScheme);
    }

    public static string FixMalformedJson(string json)
    {
        var verbatimStringRegex = new Regex(@"@?""([^""\\]|\\.)*""", RegexOptions.Multiline);

        var newJson = verbatimStringRegex.Replace(json, match =>
        {
            var unescaped = match.Value.Trim();
            if (unescaped.StartsWith("@")) unescaped = unescaped.Substring(1);
            var pattern = @"\\(?!"")(.)";
            unescaped = Regex.Replace(unescaped, pattern, @"\\$1");

            unescaped = unescaped //.Substring(2, match.Value.Length - 3) // Remove leading @ and trailing "
                //.Replace(@"\", @"\\")
                .Replace("\"\"", "\\\"") // Replace double quotes
                .Replace("\r\n", "\\n") // Replace newlines
                .Replace("\n", "\\n"); // Replace newlines (alternative format)
            return unescaped; // Add enclosing quotes
        });
        return newJson;
    }

    public new string GetRequiredResponse(string scheme)
    {
        return $"You MUST respond in JSON, scheme:\r\n {scheme}";
    }
}