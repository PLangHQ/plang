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

    /// <summary>One step down: an option of this class (<c>.os</c>), else what this setting answers next
    /// (<see cref="Next"/>).</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
    {
        if (Option(key) is { } option)
            return new global::app.data.@this(key, option.GetValue(this), parent: parent);
        return await Next(parent, key);
    }

    /// <summary>What a key that isn't one of this class's options names — a module's settings answer an
    /// action (<c>%!llm.query%</c>), an action's its options; a class has nothing past its options.</summary>
    protected virtual System.Threading.Tasks.ValueTask<global::app.data.@this> Next(global::app.data.@this parent, string key)
        => System.Threading.Tasks.ValueTask.FromResult(parent.Context.NotFound(key));

    /// <summary>Writes an option for this run: the value lands in the writer's settings under this class's
    /// path (<c>set %!app.goal.list.setting.os% = true</c> → <c>goal.list.setting.os</c>), where the next read
    /// builds it from — and on this instance.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(string key, bool isIndex,
        object? value, global::app.actor.context.@this context)
    {
        if (Option(key) == null)
            throw new System.NotSupportedException($"setting '{Path}' has no option '{key}'");
        // onto this instance through the one convert walk (a choice from its text) — a value the option can't
        // take is refused before this run holds it
        var raw = value is global::app.data.@this held ? await held.Value() : value;
        var applied = Apply(new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase) { [key] = raw }, context);
        if (!applied.Success) throw new System.NotSupportedException(applied.Error!.Message);
        await Write(key, value, context);
        return this;
    }

    /// <summary>This run's value for <paramref name="key"/> under this setting's path, in the writer's
    /// settings. The value is read now — a setting is built from this run's values in memory — and
    /// goes in whole.</summary>
    protected async System.Threading.Tasks.ValueTask Write(string key, object? value, global::app.actor.context.@this context)
    {
        var written = value as global::app.data.@this ?? new global::app.data.@this(key, value, context: context);
        await written.Value();
        await context.Setting.Set($"{Path}.{key}", written);
    }

    /// <summary>
    /// Takes raw <paramref name="values"/> into this setting's options — the one convert walk this run's
    /// values, a saved row's options and a call's own go through. Each leaf converts through the plang
    /// catalog; a nested dict onto an owned composite descends field by field, making the child when
    /// absent. A key that is no option, or a value its option can't take, is the error.
    /// </summary>
    public global::app.data.@this Apply(IDictionary<string, object?> values, global::app.actor.context.@this context)
        => Apply(this, values, context);

    private global::app.data.@this Apply(object node, IDictionary<string, object?> values, global::app.actor.context.@this context)
    {
        foreach (var kvp in values)
        {
            var prop = node.GetType().GetProperty(kvp.Key,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (prop?.SetMethod?.IsPublic != true)
                return context.Error(new global::app.error.Error(
                    $"Unknown setting '{kvp.Key}' on {node.GetType().Name} — no public-settable property.",
                    "UnknownSetting", 400));

            if (kvp.Value is IDictionary<string, object?> sub && IsComposite(prop.PropertyType))
            {
                var child = prop.GetValue(node) ?? Construct(prop.PropertyType, context);
                var r = Apply(child, sub, context);
                if (!r.Success) return r;
                prop.SetValue(node, child);
            }
            else
            {
                // Lift the raw setting to its plang value. A plang-typed property (a native
                // list<path>, a number) stores the plang value DIRECTLY — no Clr boundary to cross; it
                // holds lazy and materializes at the CONSUMER's door (row.Value<path>()). Only a CLR
                // slot (bool/string) or a typed-generic plang slot the born native value can't fit
                // lowers via Clr (the value owns its projection).
                object? val;
                try
                {
                    var built = global::app.type.item.@this.Create(kvp.Value, context);
                    // a plang-typed slot the born value doesn't fit (a choice from its text) is made by
                    // the slot's own type, through the type door
                    if (typeof(global::app.type.item.@this).IsAssignableFrom(prop.PropertyType)
                        && !prop.PropertyType.IsInstanceOfType(built)
                        && context.App.type.list[prop.PropertyType].Create(kvp.Value, new global::app.data.@this(kvp.Key, context: context)) is { } made
                        && prop.PropertyType.IsInstanceOfType(made))
                        built = made;
                    val = typeof(global::app.type.item.@this).IsAssignableFrom(prop.PropertyType)
                          && prop.PropertyType.IsInstanceOfType(built)
                        ? built
                        : built.Clr(prop.PropertyType);
                }
                catch (System.Exception ex) when (ex is System.InvalidCastException or System.FormatException
                    or System.OverflowException or System.NotSupportedException)
                {
                    return context.Error(new global::app.error.Error(
                        $"setting '{kvp.Key}' cannot bind to {prop.PropertyType.Name}: {ex.Message}",
                        "TypeConversionFailed", 400) { Exception = ex });
                }
                prop.SetValue(node, val);
            }
        }
        return context.Ok();
    }

    // A class with public setters that isn't a plang leaf (string/primitive/enum/collection) — the walk
    // descends into it.
    private bool IsComposite(System.Type t)
    {
        var u = System.Nullable.GetUnderlyingType(t) ?? t;
        if (u.IsPrimitive || u.IsEnum || u == typeof(string) || u == typeof(decimal)) return false;
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(u)) return false;
        if (!u.IsClass) return false;
        foreach (var p in u.GetProperties())
            if (p.SetMethod?.IsPublic == true) return true;
        return false;
    }

    // A composite the walk found null: one that takes a context gets the asker's; else parameterless.
    private object Construct(System.Type t, global::app.actor.context.@this context)
    {
        var withContext = t.GetConstructor(new[] { typeof(global::app.actor.context.@this) });
        return withContext != null
            ? withContext.Invoke(new object[] { context })
            : System.Activator.CreateInstance(t)!;
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
