namespace app.module.code.dll.loaded;

/// <summary>
/// A DLL loaded for its providers and plang types (<c>load code 'my-crypto.dll'</c>): taken in again, its types join the
/// app's and its providers are registered, each remembering the DLL.
/// </summary>
[global::app.Attributes.PlangType("code.dll.loaded")]
public sealed class @this : dll.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A loaded DLL's record, born holding <paramref name="type"/> — the app's.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>Plang keeps it for itself: no step names it.</summary>
    public static new bool Internal => true;

    internal override System.Threading.Tasks.Task<global::app.data.@this> Start(code.@this code, global::app.actor.context.@this context)
        => code.Load(Source, context, (assembly, types) => code.Register(assembly, types, Source, context));

    /// <summary>The pure core — a record passes through; nothing else converts into one.</summary>
    public static new @this? Create(object? raw) => raw as @this;

    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => Create(raw);
}
