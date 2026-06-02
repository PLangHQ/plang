using System.Reflection;
using app.error;

namespace app.module.code;

public sealed partial class @this : ISnapshot
{
    /// <summary>
    /// Snapshot record for one runtime registration. Type is the AssemblyQualifiedName
    /// of the provider interface (so referent integrity survives across processes);
    /// Name is the provider instance's Name; Source is the DLL path that loaded it,
    /// or null for an in-process registration on the same App.
    /// </summary>
    internal sealed record Registration(string TypeName, string ProviderName, string? Source);

    /// <summary>
    /// Snapshot record for one default-selection override — captures only when the
    /// current default differs from the corresponding built-in default for that type.
    /// </summary>
    internal sealed record DefaultOverride(string TypeName, string ProviderName);

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
                    registrations.Add(new Registration(type.AssemblyQualifiedName ?? type.FullName!, name, provider.Source));
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
                overrides.Add(new DefaultOverride(type.AssemblyQualifiedName ?? type.FullName!, currentDefaultName));
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
    public static void Restore(global::app.snapshot.@this s, global::app.actor.context.@this context)
    {
        var providers = context.App.Code;

        var registrations = s.Read<List<Registration>>("registrations") ?? new();
        var overrides = s.Read<List<DefaultOverride>>("defaultOverrides") ?? new();

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
                var dllPath = global::app.type.path.@this.Resolve(sourceForResolve, context);
                var loadResult = dllPath.LoadAssemblyAsync().GetAwaiter().GetResult();
                if (!loadResult.Success)
                    throw new System.IO.FileNotFoundException(
                        loadResult.Error?.Message ?? "Provider source DLL not loadable.");
                assembly = loadResult.Value!;
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

    public static void Read(global::app.snapshot.Io io, global::app.snapshot.@this section)
    {
        section.Write("registrations", io.Get<List<Registration>>("registrations") ?? new());
        section.Write("defaultOverrides", io.Get<List<DefaultOverride>>("defaultOverrides") ?? new());
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
