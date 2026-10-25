namespace app.origin;

/// <summary>
/// What an app is made from — the code (an app the command line, a host or a test makes) or an actor (a sub app one of
/// its actors made). Each of the app's lists is a layer over what its origin answers; the code is the bottom, with
/// nothing under it. The app asks its origin once for each part as it is born, and its layers ask it for what goes
/// below them.
/// </summary>
internal interface @this
{
    /// <summary>The app's life: the code's own; an actor's, ending with the actor's.</summary>
    System.Threading.CancellationTokenSource Shutdown();

    /// <summary>The app's disk, holding the folder at <paramref name="absolutePath"/> as what <c>/</c> is: the code's
    /// app may be anywhere; a sub app is made in its maker's folder or under it, and anywhere else is refused.</summary>
    global::app.type.item.path.file.filesystem.@this Filesystem(string absolutePath);

    /// <summary>Whether the app's System and User are born with the console's standard channels (an input that reads as
    /// EOF): the code, as the app it is made for was asked to be; an actor, always — a sub app is never the terminal's
    /// owner, which wires its own.</summary>
    bool Console { get; }

    /// <summary>The types the app's type list is a layer over: the code's own; an actor's app's.</summary>
    global::app.type.list.@this type { get; }

    /// <summary>The actions the registry of <paramref name="app"/> is a layer over: the code's own, born with
    /// <paramref name="app"/>, which answers for them; an actor's app's.</summary>
    global::app.module.@this Module(global::app.@this app);

    /// <summary>The goal at <paramref name="source"/> as <paramref name="app"/> takes it from what it is made from —
    /// the code has none; an actor's app's, while <paramref name="app"/> holds no code of its own and shares its folder.
    /// Null when it takes none.</summary>
    System.Threading.Tasks.Task<global::app.data.@this?> Goal(global::app.type.item.path.@this source, global::app.@this app);

    /// <summary>Who <paramref name="app"/> is while its store holds no identity: the code, the one its System is making
    /// now; an actor, the actor's.</summary>
    global::app.module.identity.Identity? Identity(global::app.@this app);

    /// <summary>A kind on a type below the app's layer: the code holds it on its own type; an actor refuses it — its
    /// app's type isn't the sub app's to change. The refusal; null when the kind is held.</summary>
    global::app.error.@this? Hold(global::app.type.kind.@this kind);

    /// <summary>An action into a module below the app's layer: the code adds it to its own module; an actor refuses
    /// it.</summary>
    void Register(global::app.module.@this module, string action, System.Type type);

    /// <summary>A module below the app's layer taken out by <paramref name="context"/>: the code takes its own out (for
    /// the sub apps over it too); an actor refuses it.</summary>
    System.Threading.Tasks.ValueTask<bool> Remove(global::app.module.@this module, global::app.actor.context.@this context);
}
