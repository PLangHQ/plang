using App.Types;
using PermissionRecord = global::App.FileSystem.Permission.@this;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using Read = global::App.FileSystem.Permission.Verb.Read;
using Write = global::App.FileSystem.Permission.Verb.Write;
using Delete = global::App.FileSystem.Permission.Verb.Delete;
using MatchMode = global::App.FileSystem.Permission.Match;

namespace App.FileSystem;

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
public partial class Path
{
    // Routing today: signed grants → sqlite, unsigned → in-memory. "a"
    // answers sign without an expiry argument because the signing layer's
    // public surface is text-only. When EnsureSigned grows an Expires
    // parameter, the "a" branch in this file should pass a far-future
    // TimeSpan (architect's "AlwaysExpiry" intent). Tracked in todos.md.

    public async Task<Data.@this> Authorize(Verb verb)
    {
        var actor = Context?.Actor
            ?? throw new InvalidOperationException("Path.Authorize requires Context.Actor");

        // In-root paths are auto-granted — the actor owns its own root.
        if (IsInRoot()) return Data.@this.Ok();

        var existing = await actor.Permission.Find(this, verb);
        if (existing != null) return Data.@this.Ok();

        // Loop, not recursion: adversarial input (a channel that keeps
        // returning garbage) would grow the async state machine without
        // bound under recursion.
        string prefix = "";
        while (true)
        {
            var question = $"{prefix}Allow {actor.Name} to {VerbLabel(verb)} {Absolute}? (y/n/a)";
            var askAction = new modules.output.ask
            {
                Context = Context,
                Question = new Data.@this<string>("", question),
            };
            var askResult = await Context!.App.RunAction(askAction, Context);

            // Stateless suspend: Type.Exit() bubbles up unchanged.
            if (askResult.Type?.ClrType.Exit() == true) return askResult;
            if (!askResult.Success) return askResult;

            var answer = askResult.Value?.ToString()?.Trim();
            switch (answer)
            {
                case "a": return await SignAndStore(actor, verb, persist: true);
                case "y": return await SignAndStore(actor, verb, persist: false);
                case "n": return Data.@this.FromError(
                    new global::App.Errors.PermissionDenied(BuildRequest(actor, verb)));
                default:
                    prefix = $"Invalid answer '{answer}'. ";
                    continue;
            }
        }
    }

    private async Task<Data.@this> SignAndStore(Actor.@this actor, Verb verb, bool persist)
    {
        var permission = BuildRequest(actor, verb);
        var data = new Data.@this<PermissionRecord>("", permission)
        {
            Context = Context,
        };
        if (persist) data.EnsureSigned();
        await actor.Permission.Add(data);
        return Data.@this.Ok();
    }

    private PermissionRecord BuildRequest(Actor.@this actor, Verb verb) => new(
        AppId: Context!.App.Id,
        Actor: actor.Name,
        Path:  Absolute,
        Verb:  verb,
        Match: MatchMode.Exact);

    private bool IsInRoot()
    {
        var fs = Context?.App.FileSystem;
        if (fs == null) return false;
        return IsUnder(fs.RootDirectory, RootComparison)
            || IsUnder(fs.OsDirectory, RootComparison);
    }

    /// <summary>
    /// Returns true when <see cref="Absolute"/> sits under (or equals)
    /// <paramref name="dir"/>. The OsDirectory check covers system-built-in
    /// goals (test, build) that live outside the actor's RootDirectory — they
    /// are runtime-owned files, not user content.
    /// </summary>
    private bool IsUnder(string? dir, StringComparison cmp)
    {
        if (string.IsNullOrEmpty(dir)) return false;
        var dirWithSep = dir.EndsWith(System.IO.Path.DirectorySeparatorChar)
            ? dir
            : dir + System.IO.Path.DirectorySeparatorChar;
        return Absolute.StartsWith(dirWithSep, cmp)
            || string.Equals(Absolute, dir, cmp);
    }

    private static string VerbLabel(Verb verb)
    {
        if (verb.Read    != null) return "read";
        if (verb.Write   != null) return "write";
        if (verb.Delete  != null) return "delete";
        return "access";
    }
}
