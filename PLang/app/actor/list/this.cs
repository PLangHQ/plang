namespace app.actor.list;

/// <summary>
/// The app's actors — a list of the two, System and User, reached at <c>app.actor.list</c>; one is picked
/// by name through the type (<c>app.actor.Get("user")</c>). System and User are its well-known members —
/// the same instances, not a second store. Owns their lifecycle: System is the cancellation root, User
/// links to its token, both dispose here.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<actor.@this>, IAsyncDisposable
{
    public @this(global::app.@this app) : this(app, null) { }

    /// <summary>The actors of an app <paramref name="caller"/> made: its System acts as the caller (its identity is
    /// the caller's), its own actor still.</summary>
    internal @this(global::app.@this app, actor.@this? caller) : base(new List<object?>(), app.type.list[typeof(@this)])
    {
        System = new actor.@this("System", app, app.ShutdownToken, null, caller);
        // the runtime's shared os folder is the system's to write: every actor reads and runs there, only it writes
        System.Permission.Stand(app.filesystem.os,
            global::app.type.item.permission.Verb.read, global::app.type.item.permission.Verb.execute,
            global::app.type.item.permission.Verb.write, global::app.type.item.permission.Verb.delete);
        // the stores' folder is the system's alone: it opens the stores there; no other actor reaches it
        System.Permission.Stand(app.filesystem.store,
            global::app.type.item.permission.Verb.read, global::app.type.item.permission.Verb.write, global::app.type.item.permission.Verb.delete);
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

    /// <summary>Where the work a step asks for runs, handed to <paramref name="then"/>: the context of the actor the
    /// step names (<paramref name="named"/>) — none named, the asker's own context (inside a task, the task's). Its
    /// actor is that context's. A name that didn't resolve is its own answer.</summary>
    public async Task<data.@this> Use(data.@this<global::app.type.item.choice.@this<Name>>? named,
        global::app.actor.context.@this asker, Func<global::app.actor.context.@this, Task<data.@this>> then)
    {
        if (named == null || await named.Given() is null) return await then(asker);
        // the typed carrier's own door: the name as written (text) is made the choice it names
        return await named.Use(name => then(this[name].Context));
    }

    public async ValueTask DisposeAsync()
    {
        await System.DisposeAsync();
        await User.DisposeAsync();
    }
}
