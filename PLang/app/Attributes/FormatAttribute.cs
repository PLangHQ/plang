namespace app.Attributes;

/// <summary>
/// A format a type reads — one of the type's kinds, declared on the type's class. The registry mints a
/// kind of the type per declaration: named <see cref="Name"/>, answering to its MIMEs and extensions.
/// An empty name is the type itself (<c>[Format("", "text/plain", ".txt")]</c> on text: plain text is
/// <c>{text}</c>, no kind). With no extension given, a named format's extension is <c>.name</c>.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class FormatAttribute : System.Attribute
{
    public string Name { get; }

    /// <summary>The MIME this format arrives as, or null when it has none of its own.</summary>
    public string? Mime { get; }

    /// <summary>The file extensions of this format, each with its dot.</summary>
    public string[] Extension { get; }

    /// <summary>Whether compressing content of this format pays — false for content that already is
    /// compressed (images, audio, video, archives).</summary>
    public bool Compressible { get; set; } = true;

    public FormatAttribute(string name, string? mime = null, params string[] extension)
    {
        Name = name;
        Mime = mime;
        Extension = extension.Length > 0 || name.Length == 0 ? extension : ["." + name];
    }
}
