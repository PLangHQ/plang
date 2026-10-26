namespace PLang.Tests;

/// <summary>
/// A part's node in a snapshot, by its place: the member path from the app (<c>Code</c>,
/// <c>actor.list.User.Context.Variable</c>), each step a node the walk named for the member that holds it.
/// </summary>
public static class SnapshotPathExtensions
{
    /// <summary>The user's variables, as a run on the user's context saves them.</summary>
    public const string UserVariable = "actor.list.User.Context.Variable";

    /// <summary>The user's call stack, as a run on the user's context saves it.</summary>
    public const string UserCall = "actor.list.User.Context.call";

    /// <summary>The node at <paramref name="path"/>, made along the way where it is missing (a test writing a part's
    /// node by hand).</summary>
    public static global::app.snapshot.@this Node(this global::app.snapshot.@this snapshot, string path)
    {
        foreach (var name in path.Split('.'))
        {
            if (snapshot.Entries.Get(name, snapshot.Context)?.Peek() is global::app.snapshot.@this node)
            {
                snapshot = node;
                continue;
            }
            var made = new global::app.snapshot.@this(snapshot.Context);
            snapshot.Entries.Set(new global::app.data.@this(name, made, context: snapshot.Context));
            snapshot = made;
        }
        return snapshot;
    }

    /// <summary>Whether a node was saved at <paramref name="path"/>.</summary>
    public static bool Holds(this global::app.snapshot.@this snapshot, string path)
    {
        foreach (var name in path.Split('.'))
        {
            if (snapshot.Entries.Get(name, snapshot.Context)?.Peek() is not global::app.snapshot.@this node) return false;
            snapshot = node;
        }
        return true;
    }
}
