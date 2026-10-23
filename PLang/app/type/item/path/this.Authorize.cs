using app.type;
using app.Utils;
using app.data;
using Verb = global::app.type.item.permission.Verb;
using MatchMode = global::app.type.item.permission.Match;

namespace app.type.item.path;

/// <summary>
/// The permission gate: every path verb asks whether the asking actor may act on the path before any I/O. The path is
/// judged where the OS lands for it (its <see cref="Location"/>: each link on the way followed), against the
/// filesystem's folders, each landed once and kept: in the root it may; in the stores' folder it may not, and nobody is
/// asked; anywhere else a grant it holds, or its answer when asked. The actor is the caller's: the same path, read by two
/// actors, checks each one. An answer's grant is on the place asked about — kept in memory for the run, or, answered
/// "always", in the actor's permission setting, signed as it crosses into the settings' store.
/// </summary>
public partial class @this
{
    /// <summary>The actor may <paramref name="verb"/> this path: in its own root it may; else a grant it holds, or its
    /// answer when asked. <paramref name="named"/>: the asking step names this path itself — written in the step, no
    /// %ref%, nothing its caller gave — and then a step of a goal that ships with plang (under the os folder,
    /// <c>/system/</c>) is trusted by its origin: granted without asking, and nothing stored. The trust is
    /// the asking step's own goal's, never a goal further up the call: a user goal an os goal calls stays the user's, and
    /// a path an os goal was handed (a parameter, a variable) is asked as any other.</summary>
    public async Task<data.@this> Authorize(Verb verb, actor.context.@this context, bool named = false)
    {
        // where the OS lands for this path when it opens it, worked out once for the whole call (a link can change
        // between calls); one that lands nowhere the gate can tell is refused
        if (Location(context.Filesystem) is not { } place) return context.Error(Loop);
        return await Authorize(verb, place, context, named);
    }

    /// <summary>The actor may <paramref name="verb"/> this path where the OS acts for it, <paramref name="place"/> — the
    /// door's own fact: where an open lands (<see cref="Location"/>), or the entry an unlink or a rename acts on
    /// (<see cref="Spot"/>). The question, and a yes's grant, are on that place.</summary>
    private protected async Task<data.@this> Authorize(Verb verb, @this place, actor.context.@this context, bool named = false)
    {
        var actor = context.Actor;

        // In its root, or a grant it holds: yes; the stores' folder with no grant: no — neither asks
        if (await place.Standing(verb, context) is { } answer) return answer;

        // What ships with plang asks nobody for what it names itself — the actor stays the user's own
        if (named && AskedByOs(context)) return context.Ok();

        return await actor.Permission.Ask(Question(verb, place, context),
            place.Request(actor, verb), context, persist => place.Keep(actor, [verb], persist, context));
    }

    /// <summary>The grant this place is asked for: the actor, this place, the verb, exactly.</summary>
    internal permission.@this Request(actor.@this actor, Verb verb) => permission.@this.Request(actor, Absolute, verb, MatchMode.exact);

    /// <summary>The actor's answer kept as one grant on this place, for each verb it was asked here: in memory for the
    /// run, or persisted (<paramref name="persist"/>, the answer "always"), signed as it crosses into the settings'
    /// store. One grant per place: a place's grant replaces the one before on it.</summary>
    internal async Task<data.@this> Keep(actor.@this actor, IEnumerable<Verb> verbs, bool persist, actor.context.@this context)
    {
        var grant = new permission.@this(actor.Name, Absolute, new HashSet<Verb>(verbs), MatchMode.exact, actor.App.type.list[typeof(permission.@this)]);
        await actor.Permission.Add(new data.@this<permission.@this>("", grant, context: context), persist);
        return context.Ok();
    }

    /// <summary>What the gate answers the actor about this place without asking: yes in its root or by a grant it holds;
    /// no in the stores' folder, whose keys nobody is asked for; null when the actor is to be asked. Asked of a place — a
    /// path's <see cref="Location"/>. Every door that asks takes this answer first.</summary>
    internal async Task<data.@this?> Standing(Verb verb, actor.context.@this context)
    {
        var filesystem = context.Filesystem;
        if (IsInRoot(filesystem)) return context.Ok();
        if (await context.Actor.Permission.Find(Absolute, verb) != null) return context.Ok();
        if (IsStore(filesystem)) return context.Error(new global::app.error.PermissionDenied(Request(context.Actor, verb)));
        return null;
    }

    // In the root: under the filesystem's root, and neither the os folder (what an actor may do there is its standing
    // grants: every actor reads and runs, only the system writes) nor the stores' folder (the system's alone).
    private bool IsInRoot(file.filesystem.@this filesystem)
        => IsUnder(filesystem.root).Value && !IsUnder(filesystem.os).Value && !IsStore(filesystem);

    // In the stores' folder, where the app's stores and an identity's key are kept — in any case, as a case-blind disk
    // opens it (.DATA is .data there).
    private bool IsStore(file.filesystem.@this filesystem) => IsUnder(filesystem.store, StringComparison.OrdinalIgnoreCase);

    // The running app's anchors: the only variables a goal that ships with plang may hold in what it names itself and
    // still be trusted — each a fact no caller can set (pinned). Every other variable, any other %!…% included
    // (settings are the asker's view, %!data% what the caller's steps left, a goal sets its own %!x%), is the caller's.
    private static readonly HashSet<string> Anchors = new(StringComparer.OrdinalIgnoreCase)
    {
        "%!app.filesystem.root%",
        "%!app.filesystem.os%",
    };

    /// <summary>A variable a step may hold and still name what it starts itself: one of the running app's anchors.</summary>
    internal static bool IsAnchor(global::app.type.item.variable.@this variable) => Anchors.Contains(variable.Text);

    /// <summary>The step asking runs in a goal under the runtime's os folder — what ships with plang. Its own goal (the
    /// call frame running the step), never one further up. A start trusted so takes nothing of the actor's settings
    /// that changes what runs (the terminal's environment).</summary>
    internal static bool AskedByOs(actor.context.@this context)
        => context.call.Goal?.Folder is { } folder && folder.Location(context.Filesystem) is { } place
           && place.IsUnder(context.Filesystem.os).Value;

    /// <summary>Where the gate finds this path, read in <paramref name="filesystem"/>: the place the OS opens for it — the
    /// path itself, unless a scheme reads it otherwise (a file: each link on the way followed). Null when it lands
    /// nowhere the gate can tell (a file: its links loop, or run past 40) — every door refuses it (<see cref="Loop"/>).</summary>
    internal virtual @this? Location(file.filesystem.@this filesystem) => this;

    /// <summary>Where the gate finds this path's entry itself, read in <paramref name="filesystem"/>: the place an unlink
    /// or a rename acts on — the path itself, unless a scheme reads it otherwise (a file: its folder's landing, and its own
    /// name, never followed). Null when its folder lands nowhere the gate can tell.</summary>
    internal virtual @this? Spot(file.filesystem.@this filesystem) => this;

    /// <summary>What the gate refuses this path with when it lands nowhere it can tell.</summary>
    internal global::app.error.@this Loop
        => new($"{Absolute} leads through links that loop or run past 40 — where it lands can't be told, so it is refused", "LinkLoop", 403);

    /// <summary>
    /// The question the actor is asked to <paramref name="verb"/> this path, whole — each scheme writes its own: what a
    /// yes means, in its words (a file under the os folder says what ships with plang; a url says what "always" keeps).
    /// <paramref name="place"/> is where the OS lands for it.
    /// </summary>
    protected virtual string Question(Verb verb, @this place, actor.context.@this context)
        => $"Allow {context.Actor.Name} to {verb} {Naming(place)}? (y/n/a)";

    /// <summary>This path as a question names it: as written, and where the OS lands for it (<paramref name="place"/>)
    /// when a link leads elsewhere — the place a yes is given on.</summary>
    private protected string Naming(@this place)
        => string.Equals(place.Absolute, Absolute, StringComparison.Ordinal) ? Absolute : $"{Absolute}, which is {place.Absolute}";
}
