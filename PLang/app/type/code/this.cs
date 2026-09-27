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
[global::app.Attributes.PlangType("code")]
[global::app.Attributes.Format("")]
[global::app.Attributes.Format("cs", "text/x-csharp")]
[global::app.Attributes.Format("js", "text/javascript")]
[global::app.Attributes.Format("ts", "text/typescript")]
[global::app.Attributes.Format("py", "text/x-python")]
[global::app.Attributes.Format("java", "text/x-java")]
[global::app.Attributes.Format("cpp", "text/x-c++src")]
[global::app.Attributes.Format("h", "text/x-chdr")]
[global::app.Attributes.Format("html", "text/html", ".html", ".htm")]
[global::app.Attributes.Format("css", "text/css")]
[global::app.Attributes.Format("go", "text/x-go")]
[global::app.Attributes.Format("rb", "text/x-ruby")]
[global::app.Attributes.Format("sh", "text/x-shellscript")]
[global::app.Attributes.Format("bat", "text/x-bat")]
[global::app.Attributes.Format("ps1", "text/x-powershell")]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.IEncode<@this>
{
    /// <summary>Code's formats are text: written as text writes.</summary>
    public static System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View? view,
        System.Text.Encoding? encoding, System.Threading.CancellationToken ct)
        => global::app.type.item.text.@this.Encode(stream, data, context, view, encoding, ct);

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

    public override System.Threading.Tasks.Task<bool> AsBooleanAsync(global::app.actor.context.@this context)
        => System.Threading.Tasks.Task.FromResult(!string.IsNullOrEmpty(Source));

    public override string ToString() => Source;

    /// <summary>The code renders itself as its source text — uniform across formats.</summary>
    public override void Write(global::app.type.format.IWriter writer) => writer.String(Source);
}
