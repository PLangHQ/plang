namespace app.type.item.setting;

/// <summary>
/// A setting — what an owner lets be set: a class of options (its public settable properties) whose
/// initializers are the defaults (<c>goal.list.setting</c>'s <c>Os</c>, llm's <c>Cache</c>). Each class is a
/// kind of this type, named by its path: its namespace under <c>app.</c>, a module's own class read as
/// the module's name (<c>module.action.llm.setting</c> → <c>llm</c>). A bare setting is a node — a path
/// that leads to settings (<c>goal</c>, <c>goal.list</c>, an action's <c>llm.query</c>). plang reaches
/// one as <c>%!path%</c>: the asker's settings build it, this run's values on it.
/// </summary>
public class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A node: the path it stands for.</summary>
    public @this(string path) => Path = path;

    /// <summary>A class of options: its path is its namespace under <c>app.</c>, a module's own class
    /// read as the module's name.</summary>
    protected @this()
    {
        var path = GetType().Namespace!.StartsWith("app.") ? GetType().Namespace![4..] : GetType().Namespace!;
        const string module = "module.action.", own = ".setting";
        Path = path.StartsWith(module) && path.EndsWith(own) ? path[module.Length..^own.Length] : path;
    }

    /// <summary>The path this setting is read by — <c>%!goal.list.setting%</c> is <c>goal.list.setting</c>.</summary>
    [Out] public string Path { get; }

    /// <summary>A setting is built by the asker's settings, never made from a value.</summary>
    public static @this? Create(object? raw, global::app.data.@this data)
    {
        if (raw is @this setting) return setting;
        data.Fail(new global::app.error.Error(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
            "a setting is read as %!path%, never made from a value.", "CreateItemDeclined", 400));
        return null;
    }

    /// <summary>A structure — written through the reflection kind, its [Out] members.</summary>
    public override bool IsLeaf => false;

    public override System.Threading.Tasks.ValueTask Output(global::app.channel.serializer.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
        => new global::app.type.item.kind.reflection.@this().Output(this, writer, mode, context);

    /// <summary>One step down: an option of this class (<c>.os</c>), else the setting the longer path
    /// names, as the asker's settings see it (<c>!llm</c> → <c>.query</c>, <c>!goal.call</c> → <c>.name</c>).</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
    {
        if (Option(key) is { } option)
            return new global::app.data.@this(key, option.GetValue(this), parent: parent);
        return await parent.Context.Setting.Get($"{Path}.{key}");
    }

    /// <summary>Writes an option for this run: the value lands in the writer's settings under this class's
    /// path (<c>set %!app.goal.list.setting.os% = true</c> → <c>goal.list.setting.os</c>), where the next read
    /// builds it from — and on this instance.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(string key, bool isIndex,
        object? value, global::app.actor.context.@this context)
    {
        if (Option(key) == null)
            throw new System.NotSupportedException($"setting '{Path}' has no option '{key}'");
        await context.Setting.Set(global::app.actor.setting.Storage.InMemory, $"{Path}.{key}",
            value as global::app.data.@this ?? new global::app.data.@this(key, value, context: context));
        return await base.Set(key, isIndex, value, context);
    }

    /// <summary>The option named <paramref name="key"/> — a public settable property this class declares
    /// (the base's own members are not options); null when there is none.</summary>
    internal System.Reflection.PropertyInfo? Option(string key)
    {
        for (var t = GetType(); t != null && t != typeof(@this); t = t.BaseType)
            if (t.GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.DeclaredOnly) is { SetMethod.IsPublic: true } option)
                return option;
        return null;
    }
}
