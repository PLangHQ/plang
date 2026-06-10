namespace app.module.debug;

/// <summary>
/// Writes one or more tags onto the surrounding (caller's) Call frame.
/// Two input shapes:
///   <c>- tag critical=true, owner=checkout</c>     → Pairs dict
///   <c>- tag "manual-checkpoint"</c>               → Label (writes Tags[label]="true")
/// No-op when CallStack.Current is null (executing outside a tracked dispatch).
/// Observability action — Cacheable=false; tags are diagnostic side-effects, not values.
///
/// Tags attach to the CALLER's frame, not this action's own frame: the user's intent
/// is to annotate the surrounding step/goal scope, and the tag-action's own Call pops
/// the moment Run() returns (its Tags would vanish from the live tree before the next
/// assertion could read them).
/// </summary>
[Action("tag", Cacheable = false)]
public partial class Tag : IContext
{
    /// <summary>
    /// Key/value pairs to merge into the current Call's Tags dict.
    /// Mutually exclusive with <see cref="Label"/> — the LLM picks the shape based on
    /// whether the user wrote <c>- tag k=v, ...</c> or <c>- tag "label"</c>.
    /// </summary>
    public partial global::app.data.@this<global::app.type.dict.@this>? Pairs { get; init; }

    /// <summary>
    /// Bare-string label form. Sets <c>Tags[Label] = "true"</c>.
    /// </summary>
    public partial global::app.data.@this<global::app.type.text.@this>? Label { get; init; }

    public Task<global::app.data.@this> Run()
    {
        // Tag the CALLER's Call, not our own — see class summary. Falls back to Current
        // if there's no caller (we're already at the root, e.g. a single-action scope).
        var target = Context.CallStack?.Current?.Caller ?? Context.CallStack?.Current;
        if (target == null) return Task.FromResult(global::app.data.@this.Ok());

        if (Pairs?.GetValue<Dictionary<string, string>>() is { } pairs)
        {
            foreach (var (key, value) in pairs)
                target.Tag(key, value);
        }
        else if ((Label?.Peek() as global::app.type.text.@this) is { } label && !string.IsNullOrEmpty(label))
        {
            target.Tag(label, "true");
        }

        return Task.FromResult(global::app.data.@this.Ok());
    }
}
