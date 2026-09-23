using System.Reflection;
using app.error;
using Registration = app.module.action.code.registration.@this;
using DefaultOverride = app.module.action.code.defaultoverride.@this;

namespace app.module.action.code;

public sealed partial class @this : ISnapshot
{
    // The two captured shapes are plang VALUES, each with its own reader:
    //   app/module/action/code/registration      — one runtime registration
    //   app/module/action/code/defaultoverride    — one default-selection override
    // They were CLR records read back by reflection, which could not construct them (positional,
    // no parameterless way in) and would have had to birth them blank and fill them afterwards.

    /// <summary>The providers' section.</summary>
    public string Section => "Providers";

    /// <summary>
    /// Captures the registry layer (NOT the provider instances themselves):
    ///  - non-built-in registrations as (typeName, providerName, source) tuples
    ///  - default-selection overrides where the current default differs from the
    ///    built-in default for that interface type
    /// Built-in registrations stay out of the snapshot — App boot's RegisterDefaults
    /// reproduces them on the fresh side.
    /// </summary>
    public void Capture(global::app.snapshot.@this s)
    {
        var registrations = new List<Registration>();
        var overrides = new List<DefaultOverride>();

        foreach (var (type, dict) in _providers)
        {
            string? currentDefaultName = null;

            foreach (var (name, provider) in dict)
            {
                if (provider.IsDefault)
                    currentDefaultName = name;
                if (!provider.IsBuiltIn)
                    registrations.Add(new Registration
                    {
                        TypeName = type.AssemblyQualifiedName ?? type.FullName!,
                        ProviderName = name,
                        Source = provider.Source,
                    });
            }

            // Compare current default to the built-in default this type was *born* with
            // (tracked at RegisterDefaults time so SetDefault can't erase the evidence).
            // If they differ, the user changed the selection — emit an override so Restore
            // re-applies it after the fresh App's RegisterDefaults reproduces the built-ins.
            // No built-in for the type → emit unconditionally so the chosen default survives.
            _builtInDefaults.TryGetValue(type, out var bornDefault);
            if (currentDefaultName != null
                && (bornDefault == null
                    || !string.Equals(currentDefaultName, bornDefault, StringComparison.OrdinalIgnoreCase)))
            {
                overrides.Add(new DefaultOverride
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
    /// The fresh App boot has already run RegisterDefaults so built-ins are present.
    /// </summary>
    public async System.Threading.Tasks.Task Restore(global::app.snapshot.@this s, global::app.actor.context.@this context)
    {
        var providers = this;

        var registrations = await Rows<Registration>(s, "registrations");
        var overrides = await Rows<DefaultOverride>(s, "defaultOverrides");

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
                var loadResult = dllPath.LoadAssemblyAsync().GetAwaiter().GetResult();
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

            // Find the implementation type matching ProviderName among types in the DLL
            // that implement the captured interface. Mirrors provider/load.cs.
            var implType = assembly.GetExportedTypes()
                .FirstOrDefault(t => providerType.IsAssignableFrom(t)
                                     && !t.IsInterface && !t.IsAbstract
                                     && InstanceName(t) == reg.ProviderName);

            if (implType == null)
                throw new ProviderRestoreException(
                    $"Provider '{reg.ProviderName}' implementing {providerType.Name} not found in '{reg.Source}'.");

            var instance = (ICode)Activator.CreateInstance(implType)!;
            instance.Source = reg.Source;

            // Register against every ICode-derived interface the type implements
            // (matches provider/load.cs behaviour).
            var interfaces = implType.GetInterfaces()
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


    /// <summary>The captured rows of one entry, each read through its own typed ask — the same door
    /// every value goes through, so nothing lowers to CLR here. Absent entry → empty, the same
    /// answer a captured empty list gives.</summary>
    private static async System.Threading.Tasks.Task<List<T>> Rows<T>(
        global::app.snapshot.@this s, string key)
        where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        var rows = new List<T>();
        var entry = s.Entries.Get(key);
        if (entry == null) return rows;
        foreach (var row in await entry.Value<global::app.type.item.list.@this>())
            if (await row.Value<T>() is { } value) rows.Add(value);
        return rows;
    }

    private static string InstanceName(System.Type implType)
    {
        // Mirror what provider/load does: instantiate transiently to read the Name.
        // This is the same parameterless-ctor contract that load already enforces.
        try
        {
            var probe = (ICode)Activator.CreateInstance(implType)!;
            return probe.Name;
        }
        catch
        {
            return implType.Name;
        }
    }
}

/// <summary>
/// Hard referent-integrity error raised when Providers.Restore can't resolve a
/// captured registration (missing source DLL, missing impl type, unresolvable
/// default-selection name). Always a hard fail — no silent fallback to system defaults.
/// </summary>
public sealed class ProviderRestoreException : Exception
{
    public ProviderRestoreException(string message) : base(message) { }
    public ProviderRestoreException(string message, Exception inner) : base(message, inner) { }
}
