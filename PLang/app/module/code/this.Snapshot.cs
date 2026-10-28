namespace app.module.code;

public sealed partial class @this
{
    // What a snapshot keeps of this registry is its own layer, never the provider instances; built-in registrations stay
    // out, a fresh app's registry holds them. A provider's kind is written by its plang name, never its C# type: the name
    // survives a version bump. Read back, the layer is held until the run goes on in this app (Take): taking a DLL in
    // again is asynchronous, and a default can name a provider only a DLL brings.
    private (global::app.type.item.list.@this? dll, global::app.type.item.dict.@this? provider, global::app.type.item.dict.@this? @default) _layer;

    /// <summary>Every DLL the run took in, in order, each by the door it came through (what came in with it: its
    /// providers, its types, its modules).</summary>
    [global::app.Snapshot]
    public global::app.type.item.list.@this dll
    {
        get => new(_dll, Context.App.type.list[typeof(global::app.type.item.list.@this)]);
        private set => _layer.dll = value;
    }

    /// <summary>The providers registered in-process (no DLL brought them), each kind's by name.</summary>
    [global::app.Snapshot]
    public global::app.type.item.dict.@this provider
    {
        get
        {
            var types = Context.App.type.list;
            var kinds = new global::app.type.item.dict.@this(types[typeof(global::app.type.item.dict.@this)]);
            if (System.Threading.Volatile.Read(ref _table) is not { } table) return kinds;
            foreach (var (type, providers) in table.Providers)
            {
                var names = providers.Where(named => !named.Value.IsBuiltIn && named.Value.Source == null)
                    .Select(named => (global::app.type.item.@this)new global::app.type.item.text.@this(named.Key, types[typeof(global::app.type.item.text.@this)]))
                    .ToList();
                if (names.Count > 0) kinds.Set(new kind.@this(type).Value, new global::app.type.item.list.@this(names, types[typeof(global::app.type.item.list.@this)]));
            }
            return kinds;
        }
        private set => _layer.provider = value;
    }

    /// <summary>Each kind's default where the run chose one: one that differs from the default the kind was born with
    /// (tracked as the built-ins are registered, so SetDefault can't erase the evidence), and, for a kind with no
    /// built-in, always.</summary>
    [global::app.Snapshot]
    public global::app.type.item.dict.@this @default
    {
        get
        {
            var types = Context.App.type.list;
            var chosen = new global::app.type.item.dict.@this(types[typeof(global::app.type.item.dict.@this)]);
            if (System.Threading.Volatile.Read(ref _table) is not { } table) return chosen;
            foreach (var (type, name) in table.Defaults)
            {
                table.BuiltIn.TryGetValue(type, out var builtIn);
                if (builtIn == null || !string.Equals(name, builtIn, StringComparison.OrdinalIgnoreCase))
                    chosen.Set(new kind.@this(type).Value, name);
            }
            return chosen;
        }
        private set => _layer.@default = value;
    }

    /// <summary>
    /// The layer a snapshot held taken in again, as the run goes on in this app — three steps, each failure answered (no
    /// fallback to the system defaults):
    ///   1) Each DLL the run took in is taken in again, in order, through <see cref="Load"/> (the Execute gate, its plang
    ///      types joined to the app's) and the door it first came through (<see cref="dll.@this"/>).
    ///   2) A provider registered in-process has nothing to be made again from: where this app holds it already (the
    ///      host registered it again) it is there; anywhere else it answers that it can't come back.
    ///   3) Set each kind's default again — the named provider must be registered.
    /// A fresh app's registry holds the built-ins, so they are present.
    /// </summary>
    internal async System.Threading.Tasks.Task<data.@this> Take(actor.context.@this context)
    {
        var (dlls, providers, defaults) = _layer;
        _layer = default;

        // Step 1 — the DLLs the run took in; a record that doesn't read is the answer
        foreach (var row in dlls?.Items(context) ?? [])
        {
            if (await row.Value<dll.@this>() is not { } record)
                return row.Error != null ? row : context.Error(new global::app.error.@this(
                    "the snapshot's 'dll' holds a row that reads as no code.dll", "SnapshotRowUnread", 400));
            var made = await record.Start(this, context);
            if (!made.Success)
            {
                var error = new global::app.error.@this($"the DLL '{record.Source}' the run took in failed to load again.", "DllNotLoaded", 500);
                error.list.Add(made.Error!);
                return context.Error(error);
            }
        }

        // Step 2 — providers registered in-process: no DLL brings one back, so the run goes on only where it is already
        // registered (the same host, which registered it again)
        foreach (var named in providers?.Rows(context) ?? [])
        {
            var kind = new data.@this("kind", named.Name, context: context);
            if (await kind.Value<global::app.type.item.choice.@this<kind.@this>>() is not { } choice) return kind;
            if (await named.Value<global::app.type.item.list.@this>() is not { } names) return named;
            foreach (var name in names.Items(context))
            {
                // a provider is a C# object named by a string: the registry's key is where the name lowers
                if (await name.Value<global::app.type.item.text.@this>() is not { } provider) return name;
                if (List(choice.Value.Interface).Any(registered => registered.Name == provider.ToString())) continue;
                return context.Error(new global::app.error.@this(
                    $"Provider '{provider}' for {named.Name} was registered in-process: no DLL brought it, so it can't come back.",
                    "ProviderInProcess", 500));
            }
        }

        // Step 3 — each kind's default
        foreach (var chosen in defaults?.Rows(context) ?? [])
        {
            var kind = new data.@this("kind", chosen.Name, context: context);
            if (await kind.Value<global::app.type.item.choice.@this<kind.@this>>() is not { } choice) return kind;
            if (await chosen.Value<global::app.type.item.text.@this>() is not { } provider) return chosen;
            var result = SetDefault(choice.Value.Interface, provider.ToString());
            if (!result.Success) return result;
        }
        return context.Ok();
    }
}
