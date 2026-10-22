using app.type;
using app.Utils;
using app.data;
using Verb = global::app.type.item.permission.Verb;
using MatchMode = global::app.type.item.permission.Match;

namespace app.type.item.path;

/// <summary>
/// Permission gate. FS methods call <c>path.Authorize(verb, context)</c> before any
/// I/O — Find existing grant, ask the actor on miss, sign + store the answer. The
/// actor is the caller's: the same path value, read by two actors, checks each one.
///
/// The known-awkward <c>BuildRequest</c>/<c>SignAndStore</c> shape is a
/// consequence of <c>output.ask</c> being text-only: the Permission gets
/// constructed once to format the question and again to seal on the answer.
/// When <c>output.ask</c> grows structured options the Permission becomes a
/// first-class option, defined once, signed once. Tracked in todos.md.
/// </summary>
public partial class @this
{
    // Routing today: signed grants → sqlite, unsigned → in-memory. "a"
    // answers sign without an expiry argument because the signing layer's
    // public surface is text-only. When EnsureSigned grows an Expires
    // parameter, the "a" branch in this file should pass a far-future
    // TimeSpan (architect's "AlwaysExpiry" intent). Tracked in todos.md.

    /// <summary>The actor may <paramref name="verb"/> this path: in its own root it may; else a grant it holds, or its
    /// answer when asked. <paramref name="named"/>: the asking step names this path itself — written in the step, no
    /// %ref%, nothing its caller gave — and then a step of a goal that ships with plang (under the os folder,
    /// <c>/system/</c>) is trusted by its origin: granted without asking, and nothing stored. The trust is
    /// the asking step's own goal's, never a goal further up the call: a user goal an os goal calls stays the user's, and
    /// a path an os goal was handed (a parameter, a variable) is asked as any other.</summary>
    public async Task<data.@this> Authorize(Verb verb, actor.context.@this context, bool named = false)
    {
        var actor = context?.Actor
            ?? throw new InvalidOperationException("Path.Authorize requires the caller's context with an actor");

        // where the OS lands for this path, worked out once for the whole call (a link can change between calls)
        var place = Location(context.FileSystem);

        // In its root, or a grant it holds: yes; the stores' folder with no grant: no — neither asks
        if (await Standing(verb, context, place) is { } answer) return answer;

        // What ships with plang asks nobody for what it names itself — the actor stays the user's own
        if (named && AskedByOs(context)) return context.Ok();

        // Schemes can append a hint — e.g. HttpPath warns when answering 'a' would persist a URL with a query
        // string verbatim to the local sqlite. Base returns "".
        var hint = AuthorizationHint(verb, context);
        var hintSuffix = string.IsNullOrEmpty(hint) ? "" : " " + hint;
        // the grant an answer gives is on the place asked about
        return await actor.Permission.Ask($"Allow {actor.Name} to {verb} {Absolute}?{hintSuffix} (y/n/a)",
            Request(actor, verb, place), context, persist => Keep(actor, verb, persist, place, context));
    }

    protected Task<data.@this> SignAndStore(actor.@this actor, Verb verb, bool persist, actor.context.@this context)
        => Keep(actor, verb, persist, Location(actor.Context.FileSystem), context);

    // The actor's answer kept as a grant on place. Nothing in memory signs: a persisted grant is signed when it crosses
    // the application/plang boundary into the settings store; persist says which.
    private async Task<data.@this> Keep(actor.@this actor, Verb verb, bool persist, string place, actor.context.@this context)
    {
        await actor.Permission.Add(new data.@this<permission.@this>("", Request(actor, verb, place), context: context), persist);
        return context.Ok();
    }

    // a grant is on the place: the request names where the OS lands for this path
    protected permission.@this BuildRequest(actor.@this actor, Verb verb) => Request(actor, verb, Location(actor.Context.FileSystem));

    private permission.@this Request(actor.@this actor, Verb verb, string place) =>
        permission.@this.Request(actor, place, verb, MatchMode.exact);

    /// <summary>What the gate answers the actor without asking: yes in its own root or by a grant it holds; no in the
    /// stores' folder (<see cref="IsStore"/>), whose keys nobody is asked for; null when the actor is to be asked. Every
    /// door that asks takes this answer first.</summary>
    private protected Task<data.@this?> Standing(Verb verb, actor.context.@this context)
        => Standing(verb, context, Location(context.FileSystem));

    // The same, for place: where the OS lands for this path, worked out once by the caller.
    private async Task<data.@this?> Standing(Verb verb, actor.context.@this context, string place)
    {
        var files = context.FileSystem;
        if (IsInRoot(files, context.App, place)) return context.Ok();
        if (await context.Actor!.Permission.Find(place, verb) != null) return context.Ok();
        if (IsStore(files, context.App, place)) return context.Error(new global::app.error.PermissionDenied(Request(context.Actor!, verb, place)));
        return null;
    }

    // In-root is under the app's own folder. The os folder is never in-root, even when an app is rooted there: what an
    // actor may do there is its standing grants (every actor reads and runs; only the system writes). Nor is the stores'
    // folder: only the system reaches it, through its standing grant.
    protected bool IsInRoot(actor.context.@this context) => IsInRoot(context.FileSystem, context.App, Location(context.FileSystem));

    // Each where the OS lands: place, and the app's folders it is held against (read once, kept).
    private bool IsInRoot(file.filesystem.@this files, global::app.@this? app, string place)
    {
        if (app == null) return false;
        if (IsUnder(files, place, app.OsAbsolutePath, RootComparison) || IsStore(files, app, place)) return false;
        return IsUnder(files, place, app.AbsolutePath, RootComparison);
    }

    /// <summary>Whether the stores' folder's place (<c>&lt;root&gt;/.data</c>, there yet or not) is this path or under it,
    /// each read where the OS lands — a folder whose move would carry the stores.</summary>
    private protected bool IsAboveStores(actor.context.@this context)
    {
        if (context.App is not { } app) return false;
        var files = context.FileSystem;
        var place = Location(files);
        var stores = files.Anchor(PathHelper.Combine(app.AbsolutePath, global::app.store.@this.Folder));
        var under = place.EndsWith(PathHelper.DirectorySeparatorChar) ? place : place + PathHelper.DirectorySeparatorChar;
        return stores.StartsWith(under, StringComparison.OrdinalIgnoreCase) || string.Equals(stores, place, StringComparison.OrdinalIgnoreCase);
    }

    // Under the stores' folder of the app's root (<root>/.data/), where its stores and an identity's key are kept — in
    // any case, as a case-blind disk opens it (.DATA is .data there).
    private bool IsStore(file.filesystem.@this files, global::app.@this? app, string place)
        => app != null && IsUnder(files, place, PathHelper.Combine(app.AbsolutePath, global::app.store.@this.Folder), StringComparison.OrdinalIgnoreCase);

    // The running app's anchors: the only variables a goal that ships with plang may hold in what it names itself and
    // still be trusted — each a fact no caller can set (pinned). Every other variable, any other %!…% included
    // (settings are the asker's view, %!data% what the caller's steps left, a goal sets its own %!x%), is the caller's.
    private static readonly HashSet<string> Anchors = new(StringComparer.OrdinalIgnoreCase)
    {
        "%!app.AbsolutePath%",
        "%!app.OsAbsolutePath%",
    };

    /// <summary>A variable a step may hold and still name what it starts itself: one of the running app's anchors.</summary>
    internal static bool IsAnchor(global::app.type.item.variable.@this variable) => Anchors.Contains(variable.Text);

    /// <summary>The step asking runs in a goal under the runtime's os folder — what ships with plang. Its own goal (the
    /// call frame running the step), never one further up. A start trusted so takes nothing of the actor's settings
    /// that changes what runs (the terminal's environment).</summary>
    internal static bool AskedByOs(actor.context.@this context)
    {
        var os = context.App?.OsAbsolutePath;
        if (string.IsNullOrEmpty(os) || context.call?.Goal?.Folder is not { } folder) return false;
        var under = os.EndsWith(PathHelper.DirectorySeparatorChar) ? os : os + PathHelper.DirectorySeparatorChar;
        return folder.Absolute.StartsWith(under, RootComparison) || string.Equals(folder.Absolute, os, RootComparison);
    }

    /// <summary>
    /// Returns true when <paramref name="place"/> (where the OS lands for this path) sits under (or equals) where
    /// <paramref name="rootCandidate"/> lands, read in <paramref name="files"/>.
    /// </summary>
    private bool IsUnder(file.filesystem.@this files, string place, string? rootCandidate, StringComparison cmp)
    {
        if (string.IsNullOrEmpty(rootCandidate)) return false;
        var root = files.Anchor(rootCandidate);
        var rootWithSeparator = root.EndsWith(PathHelper.DirectorySeparatorChar) ? root : root + PathHelper.DirectorySeparatorChar;
        return place.StartsWith(rootWithSeparator, cmp) || string.Equals(place, root, cmp);
    }

    /// <summary>Where the gate finds this path, read in <paramref name="files"/>: the place the OS opens for it — its
    /// absolute form, unless a scheme reads it otherwise (a file: each link on the way followed).</summary>
    internal virtual string Location(file.filesystem.@this files) => Absolute;

    /// <summary>
    /// Extra text appended to the Authorize prompt before the y/n/a choices: what a yes means beyond the words.
    /// A change under the os folder (write, delete) changes what ships with plang — and a goal there runs programs
    /// without asking, so the question says so — it asks, it doesn't refuse. HttpPath overrides to warn when an
    /// 'a' would persist a URL with query-string secrets verbatim to the local sqlite.
    /// </summary>
    protected virtual string AuthorizationHint(Verb verb, actor.context.@this context)
        => verb is Verb.write or Verb.delete
           && IsUnder(context.FileSystem, Location(context.FileSystem), context.App?.OsAbsolutePath, RootComparison)
            ? "(files under /system/ ship with plang; a goal there runs programs without asking)"
            : "";
}
