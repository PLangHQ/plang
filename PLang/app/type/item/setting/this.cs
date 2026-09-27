namespace app.type.item.setting;

/// <summary>
/// A setting — what an owner lets be set: a class of options (its public settable properties) whose
/// initializers are the defaults (<c>app.goal.list.setting</c>'s <c>Os</c>, llm's <c>Cache</c>). Each class
/// is a kind of this type, named by its path: its namespace, which is its owner's path in plang
/// (<c>%!app.goal.list.setting%</c>); a module's own class is read by the module's name
/// (<c>app.module.action.llm.setting</c> → <c>llm</c>, <c>%!llm.cache%</c>). A bare setting is a node — a
/// path that leads to settings (a module, an action's <c>llm.query</c>). The asker's settings build one,
/// the saved row and this run's values on it.
/// </summary>
public class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A node: the path it stands for.</summary>
    public @this(string path) => Path = path;

    /// <summary>A class of options: its path is its namespace, a module's own class read as the module's
    /// name.</summary>
    protected @this()
    {
        var path = GetType().Namespace!;
        const string module = "app.module.action.", own = ".setting";
        Path = path.StartsWith(module) && path.EndsWith(own) ? path[module.Length..^own.Length] : path;
    }

    /// <summary>The path this setting is read by — <c>%!app.goal.list.setting%</c> is <c>app.goal.list.setting</c>.</summary>
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

    /// <summary>A setting's type is <c>setting</c> with its path as the kind — <c>{setting, goal.list.setting}</c>:
    /// the kind is what tells a saved row which class to read back.</summary>
    protected internal override global::app.type.@this Type => new("setting", GetType(), Path);

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
        await context.Setting.Set($"{Path}.{key}",
            value as global::app.data.@this ?? new global::app.data.@this(key, value, context: context));
        return await base.Set(key, isIndex, value, context);
    }

    /// <summary>The option named <paramref name="key"/> — a public settable property this class declares
    /// (the base's own members are not options); null when there is none.</summary>
    internal System.Reflection.PropertyInfo? Option(string key)
        => Options.FirstOrDefault(o => string.Equals(o.Name, key, System.StringComparison.OrdinalIgnoreCase));

    /// <summary>This class's options — the public settable properties it declares (the base's own
    /// members are not options).</summary>
    internal IEnumerable<System.Reflection.PropertyInfo> Options
    {
        get
        {
            for (var t = GetType(); t != null && t != typeof(@this); t = t.BaseType)
                foreach (var option in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                             | System.Reflection.BindingFlags.DeclaredOnly))
                    if (option.SetMethod?.IsPublic == true) yield return option;
        }
    }
}
