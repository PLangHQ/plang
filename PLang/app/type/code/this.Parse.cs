namespace app.type.code;

/// <summary>
/// String → code. Language is detected from the source via simple
/// heuristics (keywords / characteristic syntax). Unrecognised input
/// resolves as language <c>"text"</c>.
/// </summary>
public sealed partial class @this
{
    public static @this? Resolve(string raw, global::app.actor.context.@this context)
    {
        if (raw == null) return null;
        var language = DetectLanguage(raw);
        return new @this(raw, language);
    }

    public static string DetectLanguage(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return "text";
        var s = source;

        if (s.Contains("using System") || s.Contains("namespace ") || s.Contains("Console.WriteLine"))
            return "csharp";
        if ((s.Contains("def ") && s.Contains(':')) || s.Contains("print(") || s.StartsWith("#!"))
            return "python";
        if (s.Contains("function ") || s.Contains("const ") || s.Contains("=>"))
            return "javascript";
        if (s.Contains("<?php")) return "php";
        if (s.Contains("package main") || s.Contains("func main")) return "go";
        if (s.Contains("fn main") || s.Contains("let mut ")) return "rust";
        if (s.Contains("SELECT ") || s.Contains("INSERT INTO") || s.Contains("CREATE TABLE"))
            return "sql";
        if (s.Contains("<html") || s.Contains("<!DOCTYPE")) return "html";
        if (s.TrimStart().StartsWith("{") && s.TrimEnd().EndsWith("}")) return "json";

        return "text";
    }
}
