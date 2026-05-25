namespace app.types.path.http;

/// <summary>
/// HttpPath-scheme derivation verbs (D1). URL semantics via <see cref="Uri"/>
/// — path separators are always <c>/</c>, scheme stays <c>http</c>/<c>https</c>.
/// Pure transformation: no requests, no AuthGate.
/// </summary>
public sealed partial class @this
{
    public override global::app.types.path.@this Parent
    {
        get
        {
            var segments = _uri.AbsolutePath.TrimEnd('/').Split('/');
            if (segments.Length <= 1) return this; // already at host root
            var newPath = string.Join('/', segments[..^1]);
            if (string.IsNullOrEmpty(newPath)) newPath = "/";
            return Rebuild(newPath);
        }
    }

    public override global::app.types.path.@this WithName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var parentPath = _uri.AbsolutePath.TrimEnd('/');
        var lastSlash = parentPath.LastIndexOf('/');
        var basePath = lastSlash >= 0 ? parentPath[..lastSlash] : "";
        return Rebuild(basePath + "/" + name);
    }

    public override global::app.types.path.@this WithExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        var path = _uri.AbsolutePath;
        var lastSlash = path.LastIndexOf('/');
        var fileName = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
        var dir = lastSlash >= 0 ? path[..(lastSlash + 1)] : "";
        var dot = fileName.LastIndexOf('.');
        var stem = dot >= 0 ? fileName[..dot] : fileName;
        var ext = extension.StartsWith('.') ? extension : (string.IsNullOrEmpty(extension) ? "" : "." + extension);
        return Rebuild(dir + stem + ext);
    }

    public override global::app.types.path.@this Combine(string child)
    {
        ArgumentException.ThrowIfNullOrEmpty(child);
        var basePath = _uri.AbsolutePath;
        if (!basePath.EndsWith('/')) basePath += "/";
        var trimmedChild = child.TrimStart('/');
        return Rebuild(basePath + trimmedChild);
    }

    public override global::app.types.path.@this InFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        var path = _uri.AbsolutePath;
        var lastSlash = path.LastIndexOf('/');
        var fileName = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
        var dir = lastSlash >= 0 ? path[..lastSlash] : "";
        return Rebuild(dir + "/" + folder + "/" + fileName);
    }

    private @this Rebuild(string newAbsolutePath)
    {
        var builder = new UriBuilder(_uri) { Path = newAbsolutePath };
        return new @this(builder.Uri.ToString(), Context);
    }
}
