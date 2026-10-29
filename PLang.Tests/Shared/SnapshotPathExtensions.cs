namespace PLang.Tests;

/// <summary>
/// What an object wrote in a snapshot, by its place: the member path from the snapshot (<c>app.code</c>,
/// <c>app.actor.list.user.context.variable.row</c>), each step the name the object wrote it under.
/// </summary>
public static class SnapshotPathExtensions
{
    /// <summary>The user's variables, as a run on the user's context saves them.</summary>
    public const string UserVariable = "app.actor.list.user.context.variable.row";

    /// <summary>The user's call stack, as a run on the user's context saves it.</summary>
    public const string UserCall = "app.actor.list.user.context.call";

    /// <summary>What was written at <paramref name="path"/>, read through the snapshot's own navigation; null when
    /// nothing was.</summary>
    public static async Task<global::app.data.@this?> At(this global::app.snapshot.@this snapshot, string path)
    {
        var at = new global::app.data.@this("snapshot", snapshot, context: snapshot.Context);
        foreach (var name in path.Split('.'))
        {
            if (await at.Value() is not { } value) return null;
            at = await value.Get(at, name);
            if (!at.Success || !at.IsInitialized) return null;
        }
        return at;
    }

    /// <summary>Whether anything was written at <paramref name="path"/>.</summary>
    public static async Task<bool> Holds(this global::app.snapshot.@this snapshot, string path)
        => await snapshot.At(path) is not null;

    /// <summary>What was written at <paramref name="path"/>, as the dict it reads as.</summary>
    public static async Task<global::app.type.item.dict.@this> Node(this global::app.snapshot.@this snapshot, string path)
        => await (await snapshot.At(path) ?? throw new System.InvalidOperationException($"nothing was written at {path}"))
            .Value<global::app.type.item.dict.@this>()
           ?? throw new System.InvalidOperationException($"what was written at {path} is no dict");
}
