using Registration = app.module.code.registration.@this;
using DefaultOverride = app.module.code.defaultoverride.@this;

namespace app.module.code;

public sealed partial class @this : ISnapshot
{
    // The two stored shapes are plang VALUES, each with its own reader:
    //   app/module/code/registration      — one runtime registration
    //   app/module/code/defaultoverride    — one default-selection override
    // A provider's kind is written by its plang name (kind.@this), never its C# type: the name survives a version bump.

    /// <summary>
    /// Writes the registry layer (NOT the provider instances themselves):
    ///  - non-built-in registrations, as (kind, providerName, source);
    ///  - default-selection overrides, where the current default differs from the built-in default for its kind.
    /// Built-in registrations stay out: a fresh app's registry holds them.
    /// </summary>
    data.@this ISnapshot.Store(global::app.snapshot.@this snapshot, actor.context.@this context)
    {
        var registrations = new List<global::app.type.item.@this>();
        var overrides = new List<global::app.type.item.@this>();
        var types = context.App.type.list;
        // what was made so far: a shipped provider nothing asked for is neither registered nor chosen, and a registry
        // never used holds neither
        var table = System.Threading.Volatile.Read(ref _table);

        if (table != null)
        {
            foreach (var (type, dict) in table.Providers)
            {
                var providerKind = new kind.@this(type).Value;
                table.Defaults.TryGetValue(type, out var @default);

                foreach (var (name, provider) in dict)
                {
                    if (!provider.IsBuiltIn)
                        registrations.Add(new Registration(types[typeof(Registration)])
                        {
                            Kind = providerKind,
                            ProviderName = name,
                            Source = provider.Source,
                        });
                }

                // Compare current default to the built-in default this kind was *born* with
                // (tracked as the built-ins are registered, so SetDefault can't erase the evidence).
                // If they differ, the user changed the selection — write an override so Start
                // re-applies it over the built-ins a fresh app's registry holds.
                // No built-in for the kind → write unconditionally so the chosen default survives.
                table.BuiltIn.TryGetValue(type, out var builtIn);
                if (@default != null
                    && (builtIn == null
                        || !string.Equals(@default, builtIn, StringComparison.OrdinalIgnoreCase)))
                {
                    overrides.Add(new DefaultOverride(types[typeof(DefaultOverride)])
                    {
                        Kind = providerKind,
                        ProviderName = @default,
                    });
                }
            }
        }

        // every DLL the run took in, by its source (the path value) and the door it came through
        var loaded = new List<global::app.type.item.@this>();
        foreach (var (source, modules, @namespace) in _loaded)
        {
            var load = new global::app.type.item.dict.@this(types[typeof(global::app.type.item.dict.@this)]);
            load.Set(new data.@this("source", source, context: context));
            load.Set(new data.@this("modules", modules, context: context));
            if (@namespace != null) load.Set(new data.@this("namespace", @namespace, context: context));
            loaded.Add(load);
        }

        var list = types[typeof(global::app.type.item.list.@this)];
        snapshot.Entries.Set(new data.@this("loaded", new global::app.type.item.list.@this(loaded, list), context: context));
        snapshot.Entries.Set(new data.@this("registrations", new global::app.type.item.list.@this(registrations, list), context: context));
        snapshot.Entries.Set(new data.@this("defaultOverrides", new global::app.type.item.list.@this(overrides, list), context: context));
        return context.Ok();
    }

    /// <summary>
    /// Three steps, each failure answered (no fallback to the system defaults):
    ///   1) Load each DLL the run took in again, in order, through <see cref="Load"/> (the Execute gate, its plang types
    ///      joined to the app's) and the door it first came through: its providers (<see cref="Register(System.Reflection.Assembly,
    ///      data.@this, global::app.type.item.path.@this, actor.context.@this)"/>) or its modules (<see cref="Add"/>).
    ///   2) Each stored registration must then be there, under its kind; one not yet there (registered in-process with a
    ///      source) is made again from its source DLL the same way, each source once.
    ///   3) Apply default-selection overrides — the named provider must be registered.
    /// A fresh app's registry holds the built-ins, so they are present.
    /// </summary>
    async System.Threading.Tasks.Task<data.@this> ISnapshot.Start(global::app.snapshot.@this snapshot, actor.context.@this context)
    {
        var registrations = await snapshot.Read<Registration>("registrations");
        var overrides = await snapshot.Read<DefaultOverride>("defaultOverrides");

        // Step 1 — the DLLs the run took in
        foreach (var load in await snapshot.Read<global::app.type.item.dict.@this>("loaded"))
        {
            var source = await load.Get("source", context)!.Value<global::app.type.item.path.@this>();
            var modules = (await load.Get("modules", context)!.Value<global::app.type.item.@bool.@this>())!.Value;
            var @namespace = load.Get("namespace", context) is { } named
                ? (await named.Value<global::app.type.item.text.@this>())?.ToString()
                : null;
            var made = await Load(source!, context, (assembly, types) => modules
                ? System.Threading.Tasks.Task.FromResult(context.Ok(Add(assembly, @namespace, source!)))
                : Register(assembly, types, source!, context));
            if (!made.Success)
            {
                var error = new global::app.error.@this($"the DLL '{source}' the run loaded failed to load again.", "ProviderRestore", 500);
                error.list.Add(made.Error!);
                return context.Error(error);
            }
        }

        // Step 2 — registrations
        var sources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            var named = new data.@this("kind", registration.Kind, context: context);
            if (await named.Value<global::app.type.item.choice.@this<kind.@this>>() is not { } providerKind) return named;
            if (List(providerKind.Value.Interface).Any(provider => provider.Name == registration.ProviderName)) continue;

            // A registration made in-process (no source) can't be made again from nothing.
            if (registration.Source == null)
                return context.Error(new global::app.error.@this(
                    $"Provider '{registration.ProviderName}' for {registration.Kind} has no Source — in-process registrations cannot be restored without a loadable origin.",
                    "ProviderRestore", 500));

            if (sources.Add(registration.Source))
            {
                // The source is an OS-absolute path; prefixed with "/" so path.Resolve treats it as OS-rooted (the //
                // convention on Linux) instead of anchoring it against the new app's root.
                var source = global::app.type.item.path.@this.Resolve(
                    registration.Source.StartsWith("/") ? "/" + registration.Source : registration.Source, context);
                var made = await Load(source, context, (assembly, types) => Register(assembly, types, source, context));
                if (!made.Success)
                {
                    var error = new global::app.error.@this(
                        $"Provider '{registration.ProviderName}' source '{registration.Source}' failed to load.", "ProviderRestore", 500);
                    error.list.Add(made.Error!);
                    return context.Error(error);
                }
            }

            if (!List(providerKind.Value.Interface).Any(provider => provider.Name == registration.ProviderName))
                return context.Error(new global::app.error.@this(
                    $"Provider '{registration.ProviderName}' of kind {registration.Kind} not found in '{registration.Source}'.", "ProviderRestore", 500));
        }

        // Step 3 — default-selection overrides
        foreach (var @override in overrides)
        {
            var named = new data.@this("kind", @override.Kind, context: context);
            if (await named.Value<global::app.type.item.choice.@this<kind.@this>>() is not { } providerKind) return named;

            var result = SetDefault(providerKind.Value.Interface, @override.ProviderName);
            if (!result.Success) return result;
        }
        return context.Ok();
    }
}
