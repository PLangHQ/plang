using app.error;

namespace app.module.action.code;

public sealed partial class @this
{
    /// <summary>
    /// The assembly at <paramref name="path"/> loaded into the app: through the path's Execute gate (a Read
    /// grant on the folder is no permission to run code from it), its plang types joined to the app's
    /// types. The one door a DLL comes in by — code.load and module.add both. Answers the assembly (a clr
    /// carrier); a missing file is NotFound.
    /// </summary>
    public async Task<data.@this> Load(global::app.type.item.path.@this path, actor.context.@this context)
    {
        var exists = await path.ExistsAsync(context);
        if (!exists.Success) return exists;
        if (!await exists.ToBooleanAsync())
            return context.Error(new ServiceError($"Not found: {path}", "NotFound", 404));
        var loaded = await path.LoadAssemblyAsync(context);
        if (!loaded.Success) return loaded;
        var types = context.App.type.list.Add((await loaded.Value()).Clr<System.Reflection.Assembly>()!, context);
        return types.Success ? loaded : types;
    }

    /// <summary>
    /// The code providers <paramref name="assembly"/> brings, registered for each code interface they
    /// implement, each remembering the DLL it came from (<paramref name="source"/>) so a snapshot can reload
    /// it. An assembly that brings neither providers nor plang types is NoProviders; a provider with no
    /// parameterless constructor is ProviderConstructor.
    /// </summary>
    public data.@this Register(System.Reflection.Assembly assembly, global::app.type.item.path.@this source,
        actor.context.@this context)
    {
        var exported = assembly.GetExportedTypes();
        var providerTypes = exported.Where(t => typeof(ICode).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract).ToList();
        if (providerTypes.Count == 0 && !exported.Any(t => typeof(global::app.type.item.@this).IsAssignableFrom(t)))
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
