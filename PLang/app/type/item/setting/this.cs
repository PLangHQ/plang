namespace app.type.item.setting;

/// <summary>
/// A setting — what an owner lets be set: a class of options (its public settable properties) whose
/// initializers are the defaults (<c>app.goal.list.setting</c>'s <c>Os</c>, llm's <c>Cache</c>). Each class
/// is a kind of this type, named by its path — the path a program reads it by, its owner's then
/// <c>.setting</c>: its namespace (<c>%!app.goal.list.setting%</c>), a module's read from the module
/// (<c>app.module.llm.setting</c> → <c>llm.setting</c>, <c>%!llm.setting.cache%</c>). A bare setting is a
/// node — the settings of a module or an action with no class of its own (<c>%!llm.query.setting%</c>). The
/// asker's settings build one, the saved row and this run's values on it.
/// </summary>
[global::app.Attributes.PlangType("setting")]
public class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A node: the path it stands for, born holding <paramref name="type"/> — the app's, handed in by its
    /// maker.</summary>
    public @this(string path, global::app.type.@this type)
        : base(type) => Path = path;

    /// <summary>A class of options, born holding its type (the app's list hands it its own class's type). Its path is
    /// its namespace — a module's read from the module, so <c>app.module.llm.setting</c> is <c>llm.setting</c>; a
    /// type's read where types are, at <c>app.type</c>, so <c>app.type.item.size.setting</c> is
    /// <c>app.type.size.setting</c>. A class declared inside what it configures (an action's:
    /// <c>app.module.llm.decider.setting</c>, the action being a class) is read the same way, through its owner:
    /// <c>llm.decider.setting</c>.</summary>
    protected @this(global::app.type.@this type) : base(type)
    {
        var path = GetType() is { DeclaringType: { } owner } declared
            ? $"{declared.Namespace}.{owner.Name}.{declared.Name}"
            : GetType().Namespace!;
        const string module = "app.module.", at = "app.type.item.";
        Path = path.StartsWith(module) ? path[module.Length..]
            : path.StartsWith(at) ? "app.type." + path[at.Length..]
            : path;
    }

    /// <summary>The path this setting is read by — <c>%!app.goal.list.setting%</c> is <c>app.goal.list.setting</c>,
    /// <c>%!llm.setting%</c> is <c>llm.setting</c>. Its identity, not one of its options: it is not written with them.</summary>
    public string Path { get; }

    /// <summary>Whether this setting is on — every setting node answers it, so any switch reads the same way
    /// (<c>%!signing.verify.setting.freshness.check.enabled%</c>) and can grow members without a rename. A node read
    /// as a bool is this; a bool written onto a node is this.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Enabled { get; set; } = true;

    /// <summary>Whether this setting is off — what <see cref="Enabled"/> is not.</summary>
    public global::app.type.item.@bool.@this Disabled => !Enabled.Value;

    /// <summary>A setting node read as a bool is whether it is on.</summary>
    public override bool IsTruthy() => Enabled.Value;

    /// <summary>A setting is its own stored record read back into its class (decision 789/790): a fresh one of the
    /// class the declaration names, each of its options taken from the record. A stored row of an older shape — not
    /// this class's record — is <c>SettingsUnreadable</c>: pre-1.0 there is no reader for it, so it is fixed by
    /// hand, deleted and set again.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this setting) return setting;
        if (raw is global::app.type.item.dict.@this record
            && type?.ClrType is { IsAbstract: false } clr && typeof(@this).IsAssignableFrom(clr)
            && clr != typeof(@this))
        {
            var fresh = (@this)System.Activator.CreateInstance(clr, data.Context.App.type.list[clr])!;
            // each option the record holds, read through the option's own type in Apply (the convert walk the
            // CLI flags take); a field that is no option of this class (the base's Enabled, a dropped option) is
            // not applied — the class takes only its own.
            var options = new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var entry in record.Entries(data.Context))
                if (fresh.Property(entry.Name) is not null) options[entry.Name] = entry.Peek();
            var applied = fresh.Apply(options, data.Context);
            if (applied.Success) return fresh;
            data.Fail(Unreadable(fresh.Path, applied.Error));
            return null;
        }
        if (raw is global::app.type.item.dict.@this)
        {
            // a record declared the base setting with a kind is an older-shape row ({setting, kind: <path>}),
            // whose key is the path its kind names — no reader for it pre-1.0, so it is fixed by hand (loud)
            if (type?.kind is { IsEmpty: false } k) { data.Fail(Unreadable(k.Name, null)); return null; }
            // a bare base setting (a node — a module/action with no class of its own) is not made FROM a record;
            // it reads as absent, as the base setting read did before 789. A clean decline (no Fail) that the
            // record reader answers with the null value, never the loud SettingsUnreadable.
            return null;
        }
        data.Fail(new global::app.error.@this(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
            "a setting is read as %!path% from its own stored record.", "CreateItemDeclined", 400));
        return null;
    }

    // A stored row that doesn't read back into its class — named by its key, with how a person fixes it by hand.
    private static global::app.error.@this Unreadable(string key, global::app.error.@this? cause)
        => new($"setting '{key}' was saved in an older shape and can't be read — delete it and set it again.",
            "SettingsUnreadable", 400) { list = cause is { } c ? new() { c } : new() };


    /// <summary>A structure — written through the reflection kind, its [Out] members.</summary>
    public override bool IsLeaf => false;

    /// <summary>One step down: an option of this class (<c>.os</c>), else what this setting answers next
    /// (<see cref="Next"/>).</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
    {
        // a setting answers its own identity and teaching — its address and its option rows — beside its options
        if (key.Equals(nameof(Path), System.StringComparison.OrdinalIgnoreCase))
            return new global::app.data.@this(key, new global::app.type.item.text.@this(Path,
                parent.Context.App.type.list[typeof(global::app.type.item.text.@this)]), parent: parent);
        if (key.Equals(nameof(Rows), System.StringComparison.OrdinalIgnoreCase))
            return new global::app.data.@this(key, Rows(parent.Context), parent: parent);
        if ((Property(key) ?? Switch(key)) is { } option)
            return new global::app.data.@this(key, option.GetValue(this), parent: parent);
        return await Next(parent, key);
    }

    // enabled and disabled — what every setting node answers, beside its options
    private System.Reflection.PropertyInfo? Switch(string key)
        => key.Equals(nameof(Enabled), System.StringComparison.OrdinalIgnoreCase) || key.Equals(nameof(Disabled), System.StringComparison.OrdinalIgnoreCase)
            ? typeof(@this).GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)
            : null;

    /// <summary>What a key that isn't one of this class's options names — an action's settings answer its
    /// options (<c>%!llm.query.setting.cache%</c>); a class has nothing past its options.</summary>
    protected virtual System.Threading.Tasks.ValueTask<global::app.data.@this> Next(global::app.data.@this parent, string key)
        => System.Threading.Tasks.ValueTask.FromResult(parent.Context.NotFound(key));

    /// <summary>Writes an option for this run: the value lands in the writer's settings under this class's
    /// path (<c>set %!app.goal.list.setting.os% = true</c> → <c>app.goal.list.setting.os</c>), where the next read
    /// builds it from. The answer is a copy with the option set; this instance — the one the settings handed out,
    /// read-only — is unchanged.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Set(string key, bool isIndex,
        object? value, global::app.actor.context.@this context)
    {
        if (Property(key) == null && Switch(key)?.SetMethod == null)
            return context.Error(new global::app.error.@this($"setting '{Path}' has no option '{key}'", "NoOption", 404));
        // onto a copy through the one convert walk (a choice from its text) — a value the option can't take is
        // refused before this run holds it
        var raw = value is global::app.data.@this given ? await given.Value() : value;
        var set = Copy();
        var applied = set.Apply(new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase) { [key] = raw }, context);
        if (!applied.Success) return context.Error(applied.Error!);
        await Write(key, value, context);
        return context.Ok(set);
    }

    /// <summary>This run's value for <paramref name="key"/> under this setting's path, in the writer's
    /// settings. The value is read now — a setting is built from this run's values in memory — and
    /// goes in whole.</summary>
    protected async System.Threading.Tasks.ValueTask Write(string key, object? value, global::app.actor.context.@this context)
    {
        var written = value as global::app.data.@this ?? new global::app.data.@this(key, value, context: context);
        await written.Value();
        context.Setting.Set($"{Path}.{key}", written);
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
                return context.Error(new global::app.error.@this(
                    $"Unknown setting '{kvp.Key}' on {(node is @this setting ? setting.Path : context.App.type.list[node.GetType()].Name)} — no public-settable property.",
                    "UnknownSetting", 400));

            if (kvp.Value is IDictionary<string, object?> sub && IsComposite(prop.PropertyType))
            {
                var child = prop.GetValue(node) ?? Construct(prop.PropertyType, context);
                var r = Apply(child, sub, context);
                if (!r.Success) return r;
                prop.SetValue(node, child);
            }
            else if (typeof(@this).IsAssignableFrom(prop.PropertyType))
            {
                // a setting node takes a bool — whether it is on — or its members; anything else is refused
                if (global::app.type.item.@this.Create(kvp.Value, context) is not global::app.type.item.@bool.@this on)
                    return context.Error(new global::app.error.@this(
                        $"setting '{kvp.Key}' is a switch: write true or false, or set its members", "TypeConversionFailed", 400));
                var child = (@this)(prop.GetValue(node) ?? Construct(prop.PropertyType, context));
                child.Enabled = on;
                prop.SetValue(node, child);
            }
            else
            {
                // Lift the raw setting to its plang value. A setting's option is a plang type (a native
                // list<path>, a number) and stores the plang value directly; it holds lazy and
                // materializes at the consumer's door (row.Value<path>()).
                object? val;
                try
                {
                    var built = global::app.type.item.@this.Create(kvp.Value, context);
                    // a slot the born value doesn't fit (a choice from its text, a list<path> from a list) is
                    // made by the slot's own type, through the type door; one it refuses with why (a culture no one
                    // has) is the answer
                    if (!prop.PropertyType.IsInstanceOfType(built))
                    {
                        var asked = new global::app.data.@this(kvp.Key, context: context);
                        if (context.App.type.list[prop.PropertyType].Make(kvp.Value, asked) is { } made
                            && prop.PropertyType.IsInstanceOfType(made))
                            built = made;
                        else if (!asked.Success)
                            return context.Error(asked.Error!);
                    }
                    // a list whose element is itself a closed type (list<choice<visibility>>) the type door can't
                    // close yet — its element kind names no single type — is re-tagged by its CLR form
                    val = prop.PropertyType.IsInstanceOfType(built) ? built : built.Clr(prop.PropertyType, context);
                }
                catch (System.Exception ex) when (ex is System.InvalidCastException or System.FormatException
                    or System.OverflowException or System.NotSupportedException)
                {
                    return context.Error(new global::app.error.@this(
                        $"setting '{kvp.Key}' cannot bind to {prop.PropertyType.Name}: {ex.Message}",
                        "TypeConversionFailed", 400) { Exception = ex });
                }
                prop.SetValue(node, val);
            }
        }
        // a member the values leave out holds its declared default, made by its type from the app ([Default("iec")] is
        // the app's iec); one that can't be null and declares none holds the empty value its own class is born as,
        // holding the app's type for it: an empty list or dict, a nested setting at its defaults, the machine's culture
        const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var nullability = new System.Reflection.NullabilityInfoContext();
        foreach (var member in node.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (member.SetMethod?.IsPublic != true || !typeof(global::app.type.item.@this).IsAssignableFrom(member.PropertyType)
                || member.GetValue(node) != null) continue;
            if (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::app.module.DefaultAttribute>(member) is { } declared)
            {
                var asked = new global::app.data.@this(member.Name, context: context);
                if (context.App.type.list[member.PropertyType].Make(declared.Value, asked) is { } made
                    && member.PropertyType.IsInstanceOfType(made))
                    member.SetValue(node, made);
                else
                    return context.Error(asked.Error ?? new global::app.error.@this(
                        $"setting '{member.Name}' declares a default its {member.PropertyType.Name} cannot be made from: {declared.Value}",
                        "TypeConversionFailed", 400));
            }
            else if (nullability.Create(member).WriteState != System.Reflection.NullabilityState.Nullable
                && member.PropertyType.GetConstructor(any, [typeof(global::app.type.@this)]) is { } empty)
                member.SetValue(node, empty.Invoke([context.App.type.list[member.PropertyType]]));
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
        => System.Activator.CreateInstance(t, context.App.type.list[t])!;

    /// <summary>The property of the option named <paramref name="key"/> — a public settable property this class
    /// declares (the base's own members are not options); null when there is none.</summary>
    internal System.Reflection.PropertyInfo? Property(string key)
        => Options.FirstOrDefault(o => string.Equals(o.Name, key, System.StringComparison.OrdinalIgnoreCase));

    /// <summary>The paths this setting goes by: its own, and it without <c>app.</c> (<c>app.channel.setting</c>,
    /// <c>channel.setting</c>).</summary>
    public IEnumerable<string> Paths
    {
        get
        {
            const string app = "app.";
            yield return Path;
            if (Path.StartsWith(app, System.StringComparison.OrdinalIgnoreCase)) yield return Path[app.Length..];
        }
    }

    /// <summary>Takes <paramref name="value"/> at <paramref name="path"/> — a path this setting goes by, then an option
    /// (<c>channel.setting.format</c>, <c>call.setting.diff.deep</c>), or its own alone with a dict of its options — onto a
    /// copy of this setting, through the one convert walk. Answers the address the value is kept at
    /// (<c>app.channel.setting.format</c>), or the refusal: a path that isn't this setting's, an option it doesn't have,
    /// or a value its option can't take.</summary>
    public global::app.data.@this<global::app.type.item.text.@this> Apply(string path, object? value, global::app.actor.context.@this context)
    {
        foreach (var own in Paths)
        {
            IDictionary<string, object?>? options;
            string address;
            // the setting's own path takes a dict of its options; an option's path takes its value
            if (string.Equals(path, own, System.StringComparison.OrdinalIgnoreCase))
            {
                options = value as IDictionary<string, object?>;
                address = Path;
            }
            else if (path.StartsWith(own + ".", System.StringComparison.OrdinalIgnoreCase))
            {
                var option = path[(own.Length + 1)..];
                options = Nested(option, value);
                address = $"{Path}.{option}";
            }
            else continue;
            if (options == null)
                return context.Error<global::app.type.item.text.@this>(new global::app.error.@this(
                    $"'{path}' is the setting {Path}: give it its options, {{\"option\": value}}, or name one ({path}.<option>).",
                    "TypeConversionFailed", 400));
            // the options must fit, on a copy: this setting is read-only
            var fits = Copy().Apply(options, context);
            return fits.Success
                ? context.Ok(new global::app.type.item.text.@this(address, context.App.type.list[typeof(global::app.type.item.text.@this)]))
                : context.Error<global::app.type.item.text.@this>(fits.Error!);
        }
        return context.Error<global::app.type.item.text.@this>(new global::app.error.@this(
            $"'{path}' is not a path of the setting {Path}.", "UnknownSetting", 400));
    }

    // An option path's value as the options it sets: format → {format: v}, diff.deep → {diff: {deep: v}}.
    private IDictionary<string, object?> Nested(string option, object? value)
    {
        var names = option.Split('.');
        IDictionary<string, object?> options = new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase) { [names[^1]] = value };
        for (var i = names.Length - 2; i >= 0; i--)
            options = new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase) { [names[i]] = options };
        return options;
    }

    /// <summary>A copy of this setting, its own to change: every option as it is, an owned setting (a composite —
    /// debug's <c>length</c>, math's <c>equal</c>) copied too, so nothing is shared with this one. What the settings
    /// hand out is read-only: a caller that changes a setting changes its copy.</summary>
    internal @this Copy()
    {
        var copy = (@this)MemberwiseClone();
        foreach (var option in Options)
            if (option.GetValue(this) is @this owned) option.SetValue(copy, owned.Copy());
        return copy;
    }

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

    /// <summary>This setting's options as the builder teaches them — each by the name a <c>%!path%</c>
    /// writes (<c>cache</c>), its type (named through <paramref name="context"/>'s types), and its default
    /// (the value itself, which writes its own text); a sensitive option shows no default. The rows the
    /// decider prompt reads from what <c>step.Setting</c> holds — moved here from the setting kind (774:
    /// the value holds what it offers). Takes the asker's context, so the value need store none.</summary>
    public global::app.type.property.list.@this Rows(global::app.actor.context.@this context)
    {
        var rows = new global::app.type.property.list.@this();
        foreach (var option in Options)
        {
            var reflected = new global::app.type.property.@this(option, context.App.type.list);
            rows.Add(new global::app.type.property.@this
            {
                Name = char.ToLowerInvariant(option.Name[0]) + option.Name[1..],
                Type = reflected.Type,
                Nullable = reflected.Nullable,
                Default = !option.IsDefined(typeof(global::app.SensitiveAttribute), inherit: true)
                          && option.GetValue(this) is global::app.type.item.@this { IsNull: false } value ? value : null,
            });
        }
        return rows;
    }
}
