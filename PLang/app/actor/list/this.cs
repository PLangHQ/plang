namespace app.actor.list;

/// <summary>
/// The app's actors — a list of the two, System and User, reached at <c>app.actor.list</c>; one is picked
/// by name through the type (<c>app.actor.Get("user")</c>). System and User are its well-known members —
/// the same instances, not a second store. Owns their lifecycle: System is the cancellation root, User
/// links to its token, both dispose here. Takes part in a snapshot as what holds the two, with nothing of its own.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<actor.@this>, IAsyncDisposable, global::app.snapshot.ISnapshot
{
    public @this(global::app.@this app) : base(new List<object?>(), app.type.list[typeof(@this)])
    {
        // each born with its role: what it stands on the app's folders is its permission view's, made at its first ask
        System = new actor.@this(new actor.role.system(), app, app.ShutdownToken, null, app.origin.Console);
        User = new actor.@this(new actor.role.user(), app, System.CancellationToken, System, app.origin.Console);
        Add(System);
        Add(User);
    }

    /// <summary>The system actor — the root of the cancellation hierarchy.</summary>
    public actor.@this System { get; }

    /// <summary>The user actor, for end-user operations. Linked to System's cancellation token.</summary>
    public actor.@this User { get; }

    /// <summary>The actor <paramref name="name"/> names — the closed set's own members.</summary>
    public actor.@this this[Name name] => name == Name.system ? System : User;

    /// <summary>Where the work a step asks for runs, handed to <paramref name="then"/>: the context of the actor the
    /// step names (<paramref name="named"/>) — none named, the asker's own context (inside a task, the task's). Its
    /// actor is that context's. A name that didn't resolve is its own answer.</summary>
    public async Task<data.@this> Use(data.@this<global::app.type.item.choice.@this<Name>>? named,
        global::app.actor.context.@this asker, Func<global::app.actor.context.@this, Task<data.@this>> then)
    {
        if (named == null || await named.Given() is null) return await then(asker);
        // the typed carrier's own door: the name as written (text) is made the choice it names
        // the named actor runs it in the asker's flow
        return await named.Use(name => asker.Run(this[name], then));
    }

    public async ValueTask DisposeAsync()
    {
        await System.DisposeAsync();
        await User.DisposeAsync();
    }
}
