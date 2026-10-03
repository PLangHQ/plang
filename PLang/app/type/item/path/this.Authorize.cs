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
    /// <c>/system/</c>) is trusted by its origin (decision 579): granted without asking, and nothing stored. The trust is
    /// the asking step's own goal's, never a goal further up the call: a user goal an os goal calls stays the user's, and
    /// a path an os goal was handed (a parameter, a variable) is asked as any other.</summary>
    public async Task<data.@this> Authorize(Verb verb, actor.context.@this context, bool named = false)
    {
        var actor = context?.Actor
            ?? throw new InvalidOperationException("Path.Authorize requires the caller's context with an actor");

        // In-root paths are auto-granted — the actor owns its own root.
        if (IsInRoot(context)) return context.Ok();

        // What ships with plang asks nobody for what it names itself (579) — the actor stays the user's own
        if (named && AskedByOs(context)) return context.Ok();

        var existing = await actor.Permission.Find(this, verb);
        if (existing != null) return context.Ok();

        // Schemes can append a hint — e.g. HttpPath warns when answering 'a' would persist a URL with a query
        // string verbatim to the local sqlite. Base returns "".
        var hint = AuthorizationHint(verb);
        var hintSuffix = string.IsNullOrEmpty(hint) ? "" : " " + hint;
        return await actor.Permission.Ask($"Allow {actor.Name} to {verb} {Absolute}?{hintSuffix} (y/n/a)",
            BuildRequest(actor, verb), context, persist => SignAndStore(actor, verb, persist, context));
    }

    protected async Task<data.@this> SignAndStore(actor.@this actor, Verb verb, bool persist, actor.context.@this context)
    {
        var grant = BuildRequest(actor, verb);
        var d = new data.@this<permission.@this>("", grant, context: context);
        // Signing is no longer in-memory: a persisted grant is signed when it
        // crosses the application/plang boundary into the settings store. The
        // caller's `persist` intent decides persisted vs in-memory.
        await actor.Permission.Add(d, persist);
        return context.Ok();
    }

    protected permission.@this BuildRequest(actor.@this actor, Verb verb) =>
        permission.@this.Request(actor.Name, Absolute, verb, MatchMode.exact);

    // A child app inherits its parent's filesystem scope: paths under the
    // parent's root are still in-root from a child's perspective. The os folder
    // is never in-root: its access is the actors' standing grants (permission
    // list, seeded at boot). The MaxDepth cap turns an accidental Parent cycle into a quiet
    // false (out-of-root) instead of an infinite loop on the Authorize hot
    // path; 16 is well above any legitimate child-app nesting.
    protected bool IsInRoot(actor.context.@this context)
    {
        var app = context.App;
        if (app == null) return false;
        const int MaxDepth = 16;
        for (int depth = 0; app != null && depth < MaxDepth; depth++)
        {
            // the runtime's shared os folder is never an actor's own, even when an app is rooted there: what an
            // actor may do there is its standing grants (every actor reads and runs; only the system writes)
            if (IsUnder(app.OsAbsolutePath, RootComparison)) return false;
            if (IsUnder(app.AbsolutePath, RootComparison)) return true;
            app = app.Parent;
        }
        return false;
    }

    /// <summary>The step asking runs in a goal under the runtime's os folder — what ships with plang. Its own goal (the
    /// call frame running the step), never one further up.</summary>
    private static bool AskedByOs(actor.context.@this context)
    {
        var os = context.App?.OsAbsolutePath;
        if (string.IsNullOrEmpty(os) || context.call?.Goal?.Folder is not { } folder) return false;
        var under = os.EndsWith(PathHelper.DirectorySeparatorChar) ? os : os + PathHelper.DirectorySeparatorChar;
        return folder.Absolute.StartsWith(under, RootComparison) || string.Equals(folder.Absolute, os, RootComparison);
    }

    /// <summary>
    /// Returns true when <see cref="Absolute"/> sits under (or equals)
    /// <paramref name="rootCandidate"/>.
    /// </summary>
    private bool IsUnder(string? rootCandidate, StringComparison cmp)
    {
        if (string.IsNullOrEmpty(rootCandidate)) return false;
        var rootWithSeparator = rootCandidate.EndsWith(PathHelper.DirectorySeparatorChar)
            ? rootCandidate
            : rootCandidate + PathHelper.DirectorySeparatorChar;
        return Absolute.StartsWith(rootWithSeparator, cmp)
            || string.Equals(Absolute, rootCandidate, cmp);
    }

    /// <summary>
    /// Scheme-specific extra text appended to the Authorize prompt before the
    /// y/n/a choices. Base returns empty. HttpPath overrides to warn when an
    /// 'a' would persist a URL with query-string secrets verbatim to the
    /// local sqlite. Subclasses can append any other
    /// scheme-specific consent signal here.
    /// </summary>
    protected virtual string AuthorizationHint(Verb verb) => "";
}
