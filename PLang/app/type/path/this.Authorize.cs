using app.type;
using app.Utils;
using app.data;
using Verb = global::app.type.permission.Verb;
using MatchMode = global::app.type.permission.Match;

namespace app.type.path;

/// <summary>
/// Permission gate. FS methods call <c>path.Authorize(verb)</c> before any
/// I/O — Find existing grant, ask the actor on miss, sign + store the answer.
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

    public async Task<data.@this> Authorize(Verb verb)
    {
        var actor = Context?.Actor
            ?? throw new InvalidOperationException("Path.Authorize requires Context.Actor");

        // In-root paths are auto-granted — the actor owns its own root.
        if (IsInRoot()) return Context!.Ok();

        var existing = await actor.Permission.Find(this, verb);
        if (existing != null) return Context!.Ok();

        // Loop, not recursion: adversarial input (a channel that keeps
        // returning garbage) would grow the async state machine without
        // bound under recursion.
        string prefix = "";
        while (true)
        {
            // Schemes can append a hint — e.g. HttpPath warns when answering
            // 'a' would persist a URL with a query string verbatim to the
            // local sqlite. Base returns "".
            var hint = AuthorizationHint(verb);
            var hintSuffix = string.IsNullOrEmpty(hint) ? "" : " " + hint;
            var question = $"{prefix}Allow {actor.Name} to {VerbLabel(verb)} {Absolute}?{hintSuffix} (y/n/a)";
            var askAction = new module.output.ask(Context!)
            {
                Question = new data.@this<global::app.type.text.@this>("", question, context: Context),
            };
            var askResult = await Context!.App.RunAction(askAction, Context);

            // Stateless suspend bubbles up unchanged. ShouldExit honors the
            // Value-side opt-out so a resolved Data<Ask> (Answer bound) flows
            // through; a pending Ask (Answer null) or a Type-only Exit Data
            // short-circuits as before.
            if (askResult.ShouldExit()) return askResult;
            if (!askResult.Success) return askResult;

            // output.ask returns Data<Ask>; the user's reply rides on Ask.Answer.
            var ask = await askResult.Value() as module.output.Ask;
            var answer = ask?.Answer?.Trim();
            switch (answer)
            {
                case "a": return await SignAndStore(actor, verb, persist: true);
                case "y": return await SignAndStore(actor, verb, persist: false);
                // No answer (closed/EOF input channel) = no consent — deny rather than
                // reprompt, or a channel that can never answer loops this forever.
                case "n" or null or "": return Context!.Error(
                    new global::app.error.PermissionDenied(BuildRequest(actor, verb)));
                default:
                    prefix = $"Invalid answer '{answer}'. ";
                    continue;
            }
        }
    }

    protected async Task<data.@this> SignAndStore(actor.@this actor, Verb verb, bool persist)
    {
        var grant = BuildRequest(actor, verb);
        var d = new data.@this<permission.@this>("", grant)
        {
            Context = Context,
        };
        // Signing is no longer in-memory: a persisted grant is signed when it
        // crosses the application/plang boundary into the settings store. The
        // caller's `persist` intent decides persisted vs in-memory.
        await actor.Permission.Add(d, persist);
        return Context!.Ok();
    }

    protected permission.@this BuildRequest(actor.@this actor, Verb verb) =>
        permission.@this.Request(actor.Name, Absolute, verb, MatchMode.Exact);

    // A child app inherits its parent's filesystem scope: paths under the
    // parent's root/os-folder are still in-root from a child's perspective.
    // The os-folder checks cover system-built-in goals (test, build) at any
    // depth. The MaxDepth cap turns an accidental Parent cycle into a quiet
    // false (out-of-root) instead of an infinite loop on the Authorize hot
    // path; 16 is well above any legitimate child-app nesting.
    protected bool IsInRoot()
    {
        var app = Context?.App;
        if (app == null) return false;
        const int MaxDepth = 16;
        for (int depth = 0; app != null && depth < MaxDepth; depth++)
        {
            if (IsUnder(app.AbsolutePath, RootComparison)
                || IsUnder(app.OsDirectory, RootComparison)
                || IsUnder(app.OsAbsolutePath, RootComparison))
                return true;
            app = app.Parent;
        }
        return false;
    }

    /// <summary>
    /// Returns true when <see cref="Absolute"/> sits under (or equals)
    /// <paramref name="rootCandidate"/>. The os-folder check covers
    /// system-built-in goals (test, build) that live outside the actor's root
    /// — they are runtime-owned files, not user content.
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

    private static string VerbLabel(Verb verb) => verb switch
    {
        Verb.Read    => "read",
        Verb.Write   => "write",
        Verb.Delete  => "delete",
        Verb.Execute => "execute",
        _ => "access",
    };

    /// <summary>
    /// Scheme-specific extra text appended to the Authorize prompt before the
    /// y/n/a choices. Base returns empty. HttpPath overrides to warn when an
    /// 'a' would persist a URL with query-string secrets verbatim to the
    /// local sqlite. Subclasses can append any other
    /// scheme-specific consent signal here.
    /// </summary>
    protected virtual string AuthorizationHint(Verb verb) => "";
}
