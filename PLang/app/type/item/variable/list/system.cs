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
    // The system variables, by name, in the order they are read: what each one is made as. A constant — readonly, no
    // row ever added or changed, the same for every app: each maker is handed the system it makes for and captures
    // nothing of an app.
    private static readonly (string Name, System.Func<system, data.@this> Make)[] Table =
    [
        ("Now", system => new data.DynamicData("Now", asker => asker.Ok(DateTimeOffset.Now), system._context, system.Datetime)),
        ("NowUtc", system => new data.DynamicData("NowUtc", asker => asker.Ok(DateTimeOffset.UtcNow), system._context, system.Datetime)),
        ("GUID", system => new data.DynamicData("GUID", asker => asker.Ok(Guid.NewGuid()), system._context,
            system._context.App.type.list[typeof(global::app.type.item.guid.@this)])),
        // the app's own identity, without its private key
        ("MyIdentity", system => new data.DynamicData("MyIdentity", asker => asker.Ok(asker.Actor.DefaultIdentity?.Public()), system._context)),
        // who the asking actor acts for: its identity's public key, else, in a local run, the app's own
        ("Identity", system => new data.DynamicData("Identity",
            asker => asker.Ok((asker.Actor.Identity ?? asker.Actor.DefaultIdentity)?.PublicKey), system._context)),
        // the app and the context this memory is, as the app's shortcuts (/system/shortcut/) read their asker's place
        ("!app", system => new data.DynamicData("!app", asker => asker.Ok(system._context.App), system._context)),
        ("!context", system => new data.DynamicData("!context", asker => asker.Ok(system._memory.Context), system._context)),
    ];

    // the memory these are under: its context is the one they are read in (a context born over a memory takes it)
    private readonly @this _memory;

    // each variable as made, by its row in the table; none until one is asked for
    private data.@this?[]? _made;

    internal system(@this memory) => _memory = memory;

    private actor.context.@this _context => _memory.Context;

    /// <summary>The system variable <paramref name="name"/> names, made on its first ask; null for any other name.</summary>
    internal data.@this? this[string name] => Row(name) is var row and >= 0 ? this[row] : null;

    // the variable of the table's row, made on its first ask; the first made is the one kept
    private data.@this this[int row]
    {
        get
        {
            var made = _made ?? System.Threading.LazyInitializer.EnsureInitialized(ref _made, static () => new data.@this?[Table.Length]);
            return made[row] ?? System.Threading.LazyInitializer.EnsureInitialized(ref made[row], () => Table[row].Make(this))!;
        }
    }

    /// <summary>Whether <paramref name="name"/> is a system variable's — asked without making it.</summary>
    internal bool Contains(string name) => Row(name) >= 0;

    /// <summary>The system variables' names, in the order they are read.</summary>
    internal IEnumerable<string> Names => Table.Select(each => each.Name);

    /// <summary>Every system variable, in the order they are read — each made as it is reached.</summary>
    internal IEnumerable<KeyValuePair<string, data.@this>> All
    {
        get
        {
            for (var row = 0; row < Table.Length; row++)
                yield return new(Table[row].Name, this[row]);
        }
    }

    private global::app.type.@this Datetime => _context.App.type.list[typeof(global::app.type.item.datetime.@this)];

    // the table's row for a name, whatever its case, as every variable's name is; -1 for none
    private static int Row(string name)
    {
        for (var row = 0; row < Table.Length; row++)
            if (@this.Comparer.Equals(Table[row].Name, name)) return row;
        return -1;
    }
}
