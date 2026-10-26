using app.Attributes;

namespace app.module.code.dll.added;

/// <summary>
/// A DLL added for its modules (<c>add module 'tools.dll'</c>): taken in again, its actions join the modules, found
/// under the same <see cref="Namespace"/>.
/// </summary>
public sealed class @this : dll.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>An added DLL's record, born holding <paramref name="type"/> — the app's.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>Plang keeps it for itself: no step names it.</summary>
    public static new bool Internal => true;

    /// <summary>The namespace its actions were found under; null when they were found everywhere in it.</summary>
    [Store, Out]
    public string? Namespace { get; init; }

    internal override System.Threading.Tasks.Task<global::app.data.@this> Start(code.@this code, global::app.actor.context.@this context)
        => code.Load(Source, context, (assembly, _) =>
            System.Threading.Tasks.Task.FromResult(context.Ok(code.Add(assembly, Namespace, Source))));

    /// <summary>The pure core — a record passes through; nothing else converts into one.</summary>
    public static new @this? Create(object? raw) => raw as @this;

    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => Create(raw);
}
