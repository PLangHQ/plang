namespace App.modules.debug;

/// <summary>
/// Writes one or more tags onto the currently-executing Call frame.
/// Two input shapes:
///   <c>- tag critical=true, owner=checkout</c>     → Pairs dict
///   <c>- tag "manual-checkpoint"</c>               → Label (writes Tags[label]="true")
/// No-op when CallStack.Current is null (executing outside a tracked dispatch).
/// Observability action — Cacheable=false; tags are diagnostic side-effects, not values.
/// </summary>
[ModuleDescription("Write tag metadata onto the current call frame for diagnostics")]
[System.ComponentModel.Description("Attach diagnostic tags (key=value pairs or a single label) to the current call")]
[Action("tag", Cacheable = false)]
public partial class Tag : IContext
{
    /// <summary>
    /// Key/value pairs to merge into the current Call's Tags dict.
    /// Mutually exclusive with <see cref="Label"/> — the LLM picks the shape based on
    /// whether the user wrote <c>- tag k=v, ...</c> or <c>- tag "label"</c>.
    /// </summary>
    public partial global::App.Data.@this<Dictionary<string, string>>? Pairs { get; init; }

    /// <summary>
    /// Bare-string label form. Sets <c>Tags[Label] = "true"</c>.
    /// </summary>
    public partial global::App.Data.@this<string>? Label { get; init; }

    public Task<global::App.Data.@this> Run()
    {
        var current = Context.CallStack?.Current;
        if (current == null) return Task.FromResult(global::App.Data.@this.Ok());

        if (Pairs?.Value is { } pairs)
        {
            foreach (var (key, value) in pairs)
                current.Tag(key, value);
        }
        else if (Label?.Value is { } label && !string.IsNullOrEmpty(label))
        {
            current.Tag(label, "true");
        }

        return Task.FromResult(global::App.Data.@this.Ok());
    }
}
