using System.Reflection;
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
                table.Defaults.TryGetValue(type, out var currentDefaultName);

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
                table.BuiltIn.TryGetValue(type, out var bornDefault);
                if (currentDefaultName != null
                    && (bornDefault == null
                        || !string.Equals(currentDefaultName, bornDefault, StringComparison.OrdinalIgnoreCase)))
                {
                    overrides.Add(new DefaultOverride(types[typeof(DefaultOverride)])
                    {
                        Kind = providerKind,
                        ProviderName = currentDefaultName,
                    });
                }
            }
        }

        var list = types[typeof(global::app.type.item.list.@this)];
        snapshot.Entries.Set(new data.@this("registrations", new global::app.type.item.list.@this(registrations, list), context: context));
        snapshot.Entries.Set(new data.@this("defaultOverrides", new global::app.type.item.list.@this(overrides, list), context: context));
        return context.Ok();
    }

    /// <summary>
    /// Two steps, each failure answered (no fallback to the system defaults):
    ///   1) Replay non-built-in registrations — load each source DLL, register the provider against every
    ///      ICode-derived interface it implements.
    ///   2) Apply default-selection overrides — the named provider must be registered.
    /// A fresh app's registry holds the built-ins, so they are present.
    /// </summary>
    async System.Threading.Tasks.Task<data.@this> ISnapshot.Start(global::app.snapshot.@this snapshot, actor.context.@this context)
    {
        var registrations = await snapshot.Read<Registration>("registrations");
        var overrides = await snapshot.Read<DefaultOverride>("defaultOverrides");

        // Step 1 — registrations
        foreach (var reg in registrations)
        {
            var named = new data.@this("kind", reg.Kind, context: context);
            if (await named.Value<global::app.type.item.choice.@this<kind.@this>>() is not { } providerKind) return named;
            var providerType = providerKind.Value.Interface;

            // A registration made in-process (no source) can't be made again from nothing.
            if (reg.Source == null)
                return context.Error(new global::app.error.@this(
                    $"Provider '{reg.ProviderName}' for {reg.Kind} has no Source — in-process registrations cannot be restored without a loadable origin.",
                    "ProviderRestore", 500));

            // The source is an OS-absolute path; prefixed with "/" so path.Resolve treats it as OS-rooted (the //
            // convention on Linux) instead of anchoring it against the new app's root. The load passes the AuthGate.
            var sourceForResolve = reg.Source.StartsWith("/") ? "/" + reg.Source : reg.Source;
            var loaded = await global::app.type.item.path.@this.Resolve(sourceForResolve, context).LoadAssemblyAsync(context);
            if (!loaded.Success)
            {
                var error = new global::app.error.@this($"Provider '{reg.ProviderName}' source '{reg.Source}' failed to load.", "ProviderRestore", 500);
                error.list.Add(loaded.Error!);
                return context.Error(error);
            }
            var assembly = loaded.Peek()!.Clr<Assembly>()!;

            // The provider among the DLL's classes that implement the stored kind, by the name it answers to:
            // each made once through its parameterless constructor (the contract provider/load enforces), the one
            // with the stored name kept. Mirrors provider/load.cs.
            var instance = assembly.GetExportedTypes()
                .Where(t => providerType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Select(t => (ICode)Activator.CreateInstance(t)!)
                .FirstOrDefault(provider => provider.Name == reg.ProviderName);

            if (instance == null)
                return context.Error(new global::app.error.@this(
                    $"Provider '{reg.ProviderName}' of kind {reg.Kind} not found in '{reg.Source}'.", "ProviderRestore", 500));

            instance.Source = reg.Source;

            // Register against every ICode-derived interface the type implements (matches provider/load.cs).
            var interfaces = instance.GetType().GetInterfaces()
                .Where(i => typeof(ICode).IsAssignableFrom(i) && i != typeof(ICode))
                .ToList();
            foreach (var iface in interfaces)
                Register(iface, instance);
        }

        // Step 2 — default-selection overrides
        foreach (var ov in overrides)
        {
            var named = new data.@this("kind", ov.Kind, context: context);
            if (await named.Value<global::app.type.item.choice.@this<kind.@this>>() is not { } providerKind) return named;

            var result = SetDefault(providerKind.Value.Interface, ov.ProviderName);
            if (!result.Success) return result;
        }
        return context.Ok();
    }
}
