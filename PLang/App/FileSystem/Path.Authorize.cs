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
    // Far-future expiry stamped on "a"-grants. Signing layer routes anything
    // with an expiry into the persistent store; null → in-memory only.
    private static readonly TimeSpan AlwaysExpiry = TimeSpan.FromDays(365 * 100);

    public async Task<Data.@this> Authorize(Verb verb, string prefix = "")
    {
        var actor = Context?.Actor
            ?? throw new InvalidOperationException("Path.Authorize requires Context.Actor");

        var existing = actor.Permission.Find(this, verb);
        if (existing != null) return Data.@this.Ok();

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
        return answer switch
        {
            "a" => SignAndStore(actor, verb, persist: true),
            "y" => SignAndStore(actor, verb, persist: false),
            "n" => Data.@this.FromError(new global::App.Errors.PermissionDenied(BuildRequest(actor, verb))),
            _ => await Authorize(verb, prefix: $"Invalid answer '{answer}'. "),
        };
    }

    private Data.@this SignAndStore(Actor.@this actor, Verb verb, bool persist)
    {
        var permission = BuildRequest(actor, verb);
        var data = new Data.@this<PermissionRecord>("", permission)
        {
            Context = Context,
        };
        if (persist) data.EnsureSigned(); // future: pass AlwaysExpiry once signing surface grows it
        actor.Permission.Add(data);
        return Data.@this.Ok();
    }

    private PermissionRecord BuildRequest(Actor.@this actor, Verb verb) => new(
        AppId: Context!.App.Id,
        Actor: actor.Name,
        Path:  Absolute,
        Verb:  verb,
        Match: MatchMode.Exact);

    private static string VerbLabel(Verb verb)
    {
        if (verb.Read    != null) return "read";
        if (verb.Write   != null) return "write";
        if (verb.Delete  != null) return "delete";
        return "access";
    }
}
