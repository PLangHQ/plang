using System.Reflection;
using app.error;
using Registration = app.module.code.registration.@this;
using DefaultOverride = app.module.code.defaultoverride.@this;

namespace app.module.code;

public sealed partial class @this : ISnapshot
{
    // The two captured shapes are plang VALUES, each with its own reader:
    //   app/module/code/registration      — one runtime registration
    //   app/module/code/defaultoverride    — one default-selection override
    // They were CLR records read back by reflection, which could not construct them (positional,
    // no parameterless way in) and would have had to birth them blank and fill them afterwards.

    /// <summary>The providers' section.</summary>
    public string Section => "Providers";

    /// <summary>
    /// Captures the registry layer (NOT the provider instances themselves):
    ///  - non-built-in registrations as (typeName, providerName, source) tuples
    ///  - default-selection overrides where the current default differs from the
    ///    built-in default for that interface type
    /// Built-in registrations stay out of the snapshot — a fresh App's registry holds
    /// them on the fresh side.
    /// </summary>
    public void Capture(global::app.snapshot.@this s)
    {
        var registrations = new List<Registration>();
        var overrides = new List<DefaultOverride>();
        var types = s.Context.App.type.list;
        // what was made so far: a shipped provider nothing asked for is neither registered nor chosen
        var rows = System.Threading.Volatile.Read(ref _rows);

        foreach (var (type, dict) in rows?.Providers ?? [])
        {
            rows!.Defaults.TryGetValue(type, out var currentDefaultName);

            foreach (var (name, provider) in dict)
            {
                if (!provider.IsBuiltIn)
                    registrations.Add(new Registration(types[typeof(Registration)])
                    {
                        TypeName = type.AssemblyQualifiedName ?? type.FullName!,
                        ProviderName = name,
                        Source = provider.Source,
                    });
            }

            // Compare current default to the built-in default this type was *born* with
            // (tracked as the built-ins are registered, so SetDefault can't erase the evidence).
            // If they differ, the user changed the selection — emit an override so Restore
            // re-applies it over the built-ins a fresh App's registry holds.
            // No built-in for the type → emit unconditionally so the chosen default survives.
            rows.BuiltIn.TryGetValue(type, out var bornDefault);
            if (currentDefaultName != null
                && (bornDefault == null
                    || !string.Equals(currentDefaultName, bornDefault, StringComparison.OrdinalIgnoreCase)))
            {
                overrides.Add(new DefaultOverride(types[typeof(DefaultOverride)])
                {
                    TypeName = type.AssemblyQualifiedName ?? type.FullName!,
                    ProviderName = currentDefaultName,
                });
            }
        }

        s.Write("registrations", registrations);
        s.Write("defaultOverrides", overrides);
    }

    /// <summary>
    /// Two-step restore:
    ///   1) Replay non-built-in registrations — load source DLLs (hard error on failure),
    ///      register the provider instances against each ICode-derived interface they implement.
    ///   2) Apply default-selection overrides — hard error if the named provider isn't registered.
    /// A fresh App's registry holds the built-ins, so they are present.
    /// </summary>
    public async System.Threading.Tasks.Task Restore(global::app.snapshot.@this s, global::app.actor.context.@this context)
    {
        var providers = this;

        var registrations = await s.Read<Registration>("registrations");
        var overrides = await s.Read<DefaultOverride>("defaultOverrides");

        // Step 1 — registrations
        foreach (var reg in registrations)
        {
            var providerType = System.Type.GetType(reg.TypeName);
            if (providerType == null)
                throw new ProviderRestoreException(
                    $"Provider interface type '{reg.TypeName}' could not be resolved — referent integrity failure during Providers.Restore.");

            // If this registration came from a DLL (source non-null), the DLL must be loadable.
            // If source is null (an in-process registration on the original App), the fresh App
            // can't synthesize the instance from nothing — that's also a hard error.
            if (reg.Source == null)
                throw new ProviderRestoreException(
                    $"Provider '{reg.ProviderName}' for {providerType.Name} has no Source — in-process registrations cannot be restored without a loadable origin.");

            Assembly assembly;
            try
            {
                // Restore is sync — sync-wait on path.LoadAssemblyAsync.
                // AuthGate fires; for snapshot-restore the original Source was
                // already gated at first load. reg.Source is an OS-absolute path;
                // prefix with "/" so path.Resolve's ValidatePath treats it as
                // OS-rooted (// convention on Linux) instead of anchoring against
                // the new App's root.
                var sourceForResolve = reg.Source.StartsWith("/") ? "/" + reg.Source : reg.Source;
                var dllPath = global::app.type.item.path.@this.Resolve(sourceForResolve, context);
                var loadResult = dllPath.LoadAssemblyAsync(context).GetAwaiter().GetResult();
                if (!loadResult.Success)
                    throw new System.IO.FileNotFoundException(
                        loadResult.Error?.Message ?? "Provider source DLL not loadable.");
                assembly = loadResult.Peek()!.Clr<System.Reflection.Assembly>()!;
            }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                throw new ProviderRestoreException(
                    $"Provider '{reg.ProviderName}' source '{reg.Source}' failed to load: {ex.Message}", ex);
            }

            // The provider among the DLL's classes that implement the captured interface, by the name it answers to:
            // each made once through its parameterless constructor (the contract provider/load enforces), the one
            // with the captured name kept. Mirrors provider/load.cs.
            var instance = assembly.GetExportedTypes()
                .Where(t => providerType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Select(t => (ICode)Activator.CreateInstance(t)!)
                .FirstOrDefault(provider => provider.Name == reg.ProviderName);

            if (instance == null)
                throw new ProviderRestoreException(
                    $"Provider '{reg.ProviderName}' implementing {providerType.Name} not found in '{reg.Source}'.");

            instance.Source = reg.Source;

            // Register against every ICode-derived interface the type implements
            // (matches provider/load.cs behaviour).
            var interfaces = instance.GetType().GetInterfaces()
                .Where(i => typeof(ICode).IsAssignableFrom(i) && i != typeof(ICode))
                .ToList();
            foreach (var iface in interfaces)
                providers.Register(iface, instance);
        }

        // Step 2 — default-selection overrides
        foreach (var ov in overrides)
        {
            var providerType = System.Type.GetType(ov.TypeName);
            if (providerType == null)
                throw new ProviderRestoreException(
                    $"Default-selection override for unknown type '{ov.TypeName}' — referent integrity failure.");

            var result = providers.SetDefault(providerType, ov.ProviderName);
            if (!result.Success)
                throw new ProviderRestoreException(
                    $"Cannot set default '{ov.ProviderName}' for {providerType.Name}: {result.Error?.Message ?? "not registered"}.");
        }
    }

}

/// <summary>
/// Hard referent-integrity error raised when Providers.Restore can't resolve a
/// captured registration (missing source DLL, missing impl type, unresolvable
/// default-selection name). Always a hard fail — no silent fallback to system defaults.
/// </summary>
public sealed class ProviderRestoreException : global::app.error.AppException
{
    public ProviderRestoreException(string message) : base(message, "ProviderRestore", 500) { }
    public ProviderRestoreException(string message, Exception inner) : base(message, inner, "ProviderRestore", 500) { }
}
