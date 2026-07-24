using Tag = app.type.item.tag.@this;
using System.Collections.Generic;

namespace app.goal.tag.list;

/// <summary>
/// The tag NODE — a goal's tags (<c>goal.Tag</c>), a plang list value (<c>list&lt;tag&gt;</c>) so a
/// reader returns it directly. PROGRAM STRUCTURE: born context-free (the graph is shared across
/// runs), it stores no context. Stamped as a build-birth fact by <c>test.tag</c>'s Build hook.
/// Sibling of <see cref="app.goal.step.action.list.@this"/> — the tag owns its own normalization and
/// case-insensitive equality (see <see cref="Tag"/>), so this node never hand-folds case.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<Tag>
{
    // Two ways to be born: EMPTY (callers Add each tag in) + ADOPT a list value's rows (value→slot).
    public @this() : base(new List<object?>()) { }
    public @this(global::app.type.item.list.@this source) : base(source) { }

    /// <summary>Clone/render keep this concrete node type, context-free.</summary>
    protected override global::app.type.item.list.@this Empty() => new @this();

    /// <summary>True when a tag equal to <paramref name="tag"/> is present — case-insensitive, since
    /// equality lives on the tag. Callers ask <c>goal.Tag.Has("skip")</c> rather than folding case.</summary>
    public bool Has(Tag tag)
    {
        for (int i = 0; i < Count; i++)
            if (this[i].Equals(tag)) return true;
        return false;
    }
}
