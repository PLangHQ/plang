namespace PLang.Tests;

/// <summary>
/// A path read the way a program reads it: <c>path.Read</c> lands the reference, and touching it reads its
/// content through the path's gate. The answer is the landed Data — its value the decoded content, or its
/// error the gate's / the read's.
/// </summary>
public static class PathTouch
{
    public static async Task<global::app.data.@this> Touch(this global::app.type.item.path.@this path,
        global::app.actor.context.@this context)
    {
        var landed = await path.Read(context);
        if (landed.Success) await landed.Value();
        return landed;
    }
}
