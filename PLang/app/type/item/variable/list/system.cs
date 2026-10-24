namespace app.type.item.variable.list;

/// <summary>
/// The system's variables under a memory — <c>%Now%</c>, <c>%NowUtc%</c>, <c>%GUID%</c>, <c>%MyIdentity%</c>,
/// <c>%Identity%</c>, and the context's own <c>%!app%</c> and <c>%!context%</c>: what a name the memory holds nothing for
/// reads last. Each is made the first time it is asked for, born with the memory's context, and computed on each read,
/// so an actor born at any time has them at once and nothing bound to a variable's set runs for them. A name set in the
/// memory shadows its system variable.
/// </summary>
internal sealed class system
{
    // the memory these are under: its context is the one they are read in (a context born over a memory takes it)
    private readonly @this _memory;
    private data.@this? _now, _nowUtc, _guid, _myIdentity, _identity, _app, _self;

    internal system(@this memory) => _memory = memory;

    private actor.context.@this _context => _memory.Context;

    /// <summary>The system variable <paramref name="name"/> names, made on its first ask; null for any other name.</summary>
    internal data.@this? this[string name]
        => Is(name, "Now") ? _now ??= new data.DynamicData("Now", asker => asker.Ok(DateTimeOffset.Now), _context, Datetime)
         : Is(name, "NowUtc") ? _nowUtc ??= new data.DynamicData("NowUtc", asker => asker.Ok(DateTimeOffset.UtcNow), _context, Datetime)
         : Is(name, "GUID") ? _guid ??= new data.DynamicData("GUID", asker => asker.Ok(Guid.NewGuid()), _context,
             _context.App.type.list[typeof(global::app.type.item.guid.@this)])
         // the app's own identity, without its private key
         : Is(name, "MyIdentity") ? _myIdentity ??= new data.DynamicData("MyIdentity", asker => asker.Ok(asker.Actor.DefaultIdentity?.Public()), _context)
         // who the asking actor acts for: its identity's public key, else, in a local run, the app's own
         : Is(name, "Identity") ? _identity ??= new data.DynamicData("Identity",
             asker => asker.Ok((asker.Actor.Identity ?? asker.Actor.DefaultIdentity)?.PublicKey), _context)
         // the app and the context this memory is, as the app's shortcuts (/system/shortcut/) read their asker's place
         : Is(name, "!app") ? _app ??= new data.DynamicData("!app", asker => asker.Ok(_context.App), _context)
         : Is(name, "!context") ? _self ??= new data.DynamicData("!context", asker => asker.Ok(_memory.Context), _context)
         : null;

    /// <summary>Every system variable, in the order they are named — each made as it is reached.</summary>
    internal IEnumerable<KeyValuePair<string, data.@this>> All
    {
        get
        {
            foreach (var name in (string[])["Now", "NowUtc", "GUID", "MyIdentity", "Identity", "!app", "!context"])
                yield return new(name, this[name]!);
        }
    }

    private global::app.type.@this Datetime => _context.App.type.list[typeof(global::app.type.item.datetime.@this)];

    // a name is a system variable's whatever its case, as every variable's name is
    private bool Is(string name, string system) => @this.Comparer.Equals(name, system);
}
