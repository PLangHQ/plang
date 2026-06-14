namespace app.type.code;

/// <summary>
/// PLang <c>code</c> value — a source-text snippet plus its language. The
/// LLM picks <c>code</c> over <c>string</c> for snippets so downstream
/// actions (run/validate/format, all follow-ups) can dispatch on
/// <see cref="Language"/>.
///
/// <para>Kind is the language (<c>"csharp"</c>, <c>"python"</c>, …) or
/// <c>"text"</c> when language can't be detected — derived at build by
/// <see cref="Build"/>.</para>
/// </summary>
public sealed partial class @this : global::app.type.item.@this
{
    public static string Example => "Console.WriteLine(\"hi\");";
    public static string Shape => "string";

    [global::app.Out, global::app.Store]
    public string Source { get; }

    [global::app.Out, global::app.Store]
    public string Language { get; }

    public @this(string source, string language)
    {
        Source = source ?? "";
        Language = string.IsNullOrEmpty(language) ? "text" : language;
    }

    public override System.Threading.Tasks.Task<bool> AsBooleanAsync()
        => System.Threading.Tasks.Task.FromResult(!string.IsNullOrEmpty(Source));

    public override string ToString() => Source;

    /// <summary>The code renders itself as its source text — uniform across formats.</summary>
    public override void Write(global::app.channel.serializer.IWriter writer) => writer.String(Source);
}
