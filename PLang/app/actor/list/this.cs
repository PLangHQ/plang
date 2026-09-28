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
        User = new actor.@this("User", app, System.CancellationToken, fallback: System);
        Add(System);
        Add(User);
    }

    /// <summary>The system actor — the root of the cancellation hierarchy.</summary>
    public actor.@this System { get; }

    /// <summary>The user actor, for end-user operations. Linked to System's cancellation token.</summary>
    public actor.@this User { get; }

    /// <summary>The actor <paramref name="name"/> names — the closed set's own members.</summary>
    public actor.@this this[Name name] => name == Name.system ? System : User;

    /// <summary>The actor a step names (<paramref name="named"/>), handed to <paramref name="then"/> — the asker's
    /// own when none is named. A name that didn't resolve is its own answer.</summary>
    public async Task<data.@this> Use(data.@this<global::app.type.item.choice.@this<Name>>? named,
        global::app.actor.context.@this asker, Func<actor.@this, Task<data.@this>> then)
    {
        if (named == null || await named.Given() is not { } given) return await then(asker.Actor);
        return await given.Use<global::app.type.item.choice.@this<Name>>(name => then(this[name]));
    }

    public async ValueTask DisposeAsync()
    {
        await System.DisposeAsync();
        await User.DisposeAsync();
    }
}
