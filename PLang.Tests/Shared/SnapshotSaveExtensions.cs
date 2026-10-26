namespace PLang.Tests;

/// <summary>
/// A save and a start through the doors production takes: the snapshot an app's save made, and an app started from one
/// (the start <c>resume</c> makes before it re-enters the frames).
/// </summary>
public static class SnapshotSaveExtensions
{
    /// <summary>The snapshot <paramref name="saved"/> holds; a refused save throws its error (a test of the refusal
    /// asks the Data).</summary>
    public static global::app.snapshot.@this Saved(this global::app.data.@this<global::app.snapshot.@this> saved)
        => saved.Success ? (global::app.snapshot.@this)saved.Peek() : throw new global::app.error.AppException(saved.Error!);

    /// <summary>Starts <paramref name="app"/> and every part under it from <paramref name="snapshot"/>; a part that
    /// can't come back throws its error.</summary>
    public static async Task Restore(this global::app.@this app, global::app.snapshot.@this snapshot,
        global::app.actor.context.@this context)
    {
        var started = await snapshot.Start(app, context);
        if (!started.Success) throw new global::app.error.AppException(started.Error!);
    }
}
