namespace app.actor.list;

/// <summary>
/// The app's actors, reached at <c>app.Actor</c>. Selects by name (<c>app.Actor[name]</c>); System
/// and User are its well-known members — the same instances, not a second store. Owns their
/// lifecycle: System is the cancellation root, User links to its token, both dispose here.
/// </summary>
public sealed class @this : IAsyncDisposable
{
    public @this(global::app.@this app, CancellationToken shutdown)
    {
        System = new actor.@this("System", app, shutdown);
        User = new actor.@this("User", app, System.CancellationToken);
    }

    /// <summary>The system actor — the root of the cancellation hierarchy.</summary>
    public actor.@this System { get; }

    /// <summary>The user actor, for end-user operations. Linked to System's cancellation token.</summary>
    public actor.@this User { get; }

    /// <summary>The actor a name selects.</summary>
    public actor.@this this[Name name] => name switch
    {
        Name.system => System,
        Name.user => User,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not an actor"),
    };

    public async ValueTask DisposeAsync()
    {
        await System.DisposeAsync();
        await User.DisposeAsync();
    }
}
