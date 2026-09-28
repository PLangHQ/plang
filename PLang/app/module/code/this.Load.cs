using app.error;

namespace app.module.code;

public sealed partial class @this
{
    /// <summary>
    /// The assembly at <paramref name="path"/> loaded into the app — through the path's Execute gate (a Read
    /// grant on the folder is no permission to run code from it; a missing file is the path's NotFound) — its
    /// plang types joined to the app's, then handed on with what the types said they took. The one door a
    /// DLL comes in by: code.load and module.add both.
    /// </summary>
    public async Task<data.@this> Load(global::app.type.item.path.@this path, actor.context.@this context,
        System.Func<System.Reflection.Assembly, data.@this, Task<data.@this>> then)
    {
        var loaded = await path.LoadAssemblyAsync(context);
        if (!loaded.Success) return loaded;
        var assembly = (await loaded.Value()).Clr<System.Reflection.Assembly>()!;
        var types = context.App.type.list.Add(assembly, context);
        return types.Success ? await then(assembly, types) : types;
    }

    /// <summary>
    /// The code providers <paramref name="assembly"/> brings, registered for each code interface they
    /// implement, each remembering the DLL it came from (<paramref name="source"/>) so a snapshot can reload
    /// it. An assembly that brought neither providers nor plang types (<paramref name="types"/>, what the
    /// app's types took from it) is NoProviders; a provider with no parameterless constructor is
    /// ProviderConstructor.
    /// </summary>
    public async Task<data.@this> Register(System.Reflection.Assembly assembly, data.@this types,
        global::app.type.item.path.@this source, actor.context.@this context)
    {
        var providerTypes = assembly.GetExportedTypes()
            .Where(t => typeof(ICode).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract).ToList();
        if (providerTypes.Count == 0 && await types.IsEmpty())
            return context.Error(new ActionError("No ICode or [PlangType] entries found in assembly", "NoProviders", 400));

        var registered = new List<ICode>();
        foreach (var type in providerTypes)
        {
            if (type.GetConstructor(System.Type.EmptyTypes) is not { } ctor)
                return context.Error(new ActionError($"Provider '{type.Name}' has no parameterless constructor", "ProviderConstructor", 400));
            var instance = (ICode)ctor.Invoke(null);
            instance.Source = source.Absolute;
            foreach (var iface in type.GetInterfaces().Where(i => typeof(ICode).IsAssignableFrom(i) && i != typeof(ICode)))
                if (Register(iface, instance) is { Success: false } refused) return refused;
            registered.Add(instance);
        }
        return context.Ok(registered);
    }
}
