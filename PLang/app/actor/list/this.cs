namespace app.actor.list;

/// <summary>
/// The app's actors — a list of the two, System and User, reached at <c>app.actor.list</c>; one is picked
/// by name through the type (<c>app.actor.Get("user")</c>). System and User are its well-known members —
/// the same instances, not a second store. Owns their lifecycle: System is the cancellation root, User
/// links to its token, both dispose here.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<actor.@this>, IAsyncDisposable
{
    public @this(global::app.@this app) : base(new List<object?>())
    {
        System = new actor.@this("System", app, app.ShutdownToken);
        User = new actor.@this("User", app, System.CancellationToken);
        Add(System);
        Add(User);
    }

    /// <summary>The system actor — the root of the cancellation hierarchy.</summary>
    public actor.@this System { get; }

    /// <summary>The user actor, for end-user operations. Linked to System's cancellation token.</summary>
    public actor.@this User { get; }

    public async ValueTask DisposeAsync()
    {
        await System.DisposeAsync();
        await User.DisposeAsync();
    }
}
