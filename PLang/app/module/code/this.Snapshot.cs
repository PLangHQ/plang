using Registration = app.module.code.registration.@this;
using Default = app.module.code.@default.@this;

namespace app.module.code;

public sealed partial class @this : ISnapshot
{
    // The stored shapes are plang VALUES, each with its own reader:
    //   app/module/code/dll               — a DLL the run took in, by the door it came through (loaded, added)
    //   app/module/code/registration      — a provider registered in-process
    //   app/module/code/default           — the provider a kind's default is set to
    // A provider's kind is written by its plang name (kind.@this), never its C# type: the name survives a version bump.

    /// <summary>
    /// Writes the registry layer (NOT the provider instances themselves):
    ///  - every DLL the run took in, in order, each by the door it came through (what came in with it: its providers,
    ///    its types, its modules);
    ///  - the providers registered in-process (no DLL brought them), by kind and name;
    ///  - each kind's default (<c>default</c>), where it differs from the one the kind was born with.
    /// Built-in registrations stay out: a fresh app's registry holds them.
    /// </summary>
    data.@this ISnapshot.Store(global::app.snapshot.@this snapshot, actor.context.@this context)
    {
        var registrations = new List<global::app.type.item.@this>();
        var defaults = new List<global::app.type.item.@this>();
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

                // a provider a DLL brought remembers the DLL (its Source), which is stored as the DLL
                foreach (var (name, provider) in dict)
                {
                    if (!provider.IsBuiltIn && provider.Source == null)
                        registrations.Add(new Registration(types[typeof(Registration)])
                        {
                            Kind = providerKind,
                            Provider = name,
                        });
                }

                // Compare current default to the built-in default this kind was *born* with
                // (tracked as the built-ins are registered, so SetDefault can't erase the evidence).
                // If they differ, the user changed the selection — write the kind's default so Start
                // sets it again over the built-ins a fresh app's registry holds.
                // No built-in for the kind → write it unconditionally so the chosen default survives.
                table.BuiltIn.TryGetValue(type, out var builtIn);
                if (@default != null
                    && (builtIn == null
                        || !string.Equals(@default, builtIn, StringComparison.OrdinalIgnoreCase)))
                {
                    defaults.Add(new Default(types[typeof(Default)])
                    {
                        Kind = providerKind,
                        Provider = @default,
                    });
                }
            }
        }

        var list = types[typeof(global::app.type.item.list.@this)];
        snapshot.Write("dll", new global::app.type.item.list.@this(_dll, list));
        snapshot.Write("registration", new global::app.type.item.list.@this(registrations, list));
        snapshot.Write("default", new global::app.type.item.list.@this(defaults, list));
        return context.Ok();
    }

    /// <summary>
    /// Three steps, each failure answered (no fallback to the system defaults):
    ///   1) Each DLL the run took in is taken in again, in order, through <see cref="Load"/> (the Execute gate, its plang
    ///      types joined to the app's) and the door it first came through (<see cref="dll.@this"/>).
    ///   2) A provider registered in-process has nothing to be made again from: it answers that it can't come back.
    ///   3) Set each kind's default again — the named provider must be registered.
    /// A fresh app's registry holds the built-ins, so they are present.
    /// </summary>
    async System.Threading.Tasks.Task<data.@this> ISnapshot.Start(global::app.snapshot.@this snapshot, actor.context.@this context)
    {
        // Step 1 — the DLLs the run took in; a record that doesn't read is its row's answer
        if (snapshot.Entries.Get("dll", context) is { } dlls)
            foreach (var row in (await dlls.Value<global::app.type.item.list.@this>())!.Items(context))
            {
                if (await row.Value<dll.@this>() is not { } dll) return row;
                var made = await dll.Start(this, context);
                if (!made.Success)
                {
                    var error = new global::app.error.@this($"the DLL '{dll.Source}' the run took in failed to load again.", "DllNotLoaded", 500);
                    error.list.Add(made.Error!);
                    return context.Error(error);
                }
            }

        // Step 2 — providers registered in-process
        if ((await snapshot.Read<Registration>("registration")).FirstOrDefault() is { } registration)
            return context.Error(new global::app.error.@this(
                $"Provider '{registration.Provider}' for {registration.Kind} was registered in-process: no DLL brought it, so it can't come back.",
                "ProviderInProcess", 500));

        // Step 3 — each kind's default
        foreach (var @default in await snapshot.Read<Default>("default"))
        {
            var named = new data.@this("kind", @default.Kind, context: context);
            if (await named.Value<global::app.type.item.choice.@this<kind.@this>>() is not { } providerKind) return named;

            var result = SetDefault(providerKind.Value.Interface, @default.Provider);
            if (!result.Success) return result;
        }
        return context.Ok();
    }
}
