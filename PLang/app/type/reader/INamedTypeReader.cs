namespace app.type.reader;

/// <summary>
/// An <see cref="ITypeReader"/> that NAMES the type it registers under, instead of inheriting
/// it from its <c>serializer</c> namespace. Discovery keys readers by the last namespace
/// segment (<c>app.type.&lt;name&gt;.serializer</c>), which can't express a dotted type name
/// like <c>goal.call</c>; such a reader declares <see cref="TypeName"/> and the registry keys
/// it there. The registry stays the single source — no hardcoded type names in it.
/// </summary>
public interface INamedTypeReader
{
    /// <summary>The type name this reader registers under (e.g. <c>goal.call</c>).</summary>
    string TypeName { get; }
}
