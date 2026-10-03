namespace app.type.item.note;

/// <summary>What a notes file is about — an action, a type's member: how a warning names it, what a line of its notes
/// names (its parts: an action's properties, a member's arguments), and which names a line may carry.</summary>
public interface IAbout
{
    /// <summary>How a warning names it: <c>file.read</c>, <c>text.replace</c>.</summary>
    string Noted { get; }

    /// <summary>What its parts are, as a warning says it: <c>property</c>, <c>argument</c>.</summary>
    string Part { get; }

    /// <summary>Its parts, each of which a line should name.</summary>
    System.Collections.Generic.IEnumerable<string> Parts { get; }

    /// <summary>Whether a line may be named <paramref name="name"/> — one of its parts, or a name it allows besides
    /// (a member's own). <c>Returns</c> is every one's.</summary>
    bool Names(string name);
}
