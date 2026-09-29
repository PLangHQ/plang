namespace app.type.item.variable;

/// <summary>
/// A variable: its <see cref="Text"/> as written (<c>%user.address[idx].city%</c>) and its
/// <see cref="Code"/>, the hops that reach what it names — like a step's text and code. It is a
/// value, so its door is <see cref="Value"/>, which starts its code; the same variable writes
/// (<see cref="Set"/>). A property that names where to write (<c>variable.set</c>'s <c>Name</c>,
/// every <c>Data&lt;variable&gt;</c> slot) holds one.
/// <para><see cref="parser.@this">The parser</see> is the only thing that makes one from text.
/// <see cref="IName"/> tells the source generator's <c>Data&lt;T&gt;</c> emit a slot names a
/// variable rather than carrying a value.</para>
/// </summary>
[global::app.Attributes.PlangType("variable")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, IName,
    global::app.type.item.IMatch<@this>, global::app.type.item.ICurrent<@this>, global::app.type.item.ILoad<@this>,
    global::app.type.item.IList<@this, global::app.type.item.list.@this<@this>>
{
    public static string Example => "%user%";
    public static string Description => "A variable, named between % signs, that holds a value.";

    /// <summary>A key names this variable by its name; case is not the program's to get right.</summary>
    public System.Threading.Tasks.ValueTask<@this?> Match(string key)
        => System.Threading.Tasks.ValueTask.FromResult(string.Equals(Name, key, System.StringComparison.OrdinalIgnoreCase) ? this : null);

    /// <summary>The variables belong to an actor, not to the app: there is no app-wide list of them.</summary>
    public static global::app.type.item.list.@this<@this> List(global::app.@this app)
        => throw new System.InvalidOperationException("a variable list belongs to an actor: use context.Variable");

    /// <summary>A variable is set and removed through the variable type's events, then its own.</summary>
    protected internal override global::app.type.item.@this? Level(int depth, global::app.actor.context.@this context) => depth switch
    {
        0 => context.App.variable,
        1 => this,
        _ => null,
    };

    /// <summary>The variables the asker sees — its actor's memory.</summary>
    public static global::app.type.item.list.@this<@this>? Of(global::app.actor.context.@this context)
        => context.Variable.list;

    /// <summary>The variable as written, with its % signs.</summary>
    [Out] public string Text { get; }

    /// <summary>The hops that reach what the variable names, the root first.</summary>
    public code.@this Code { get; }

    internal @this(string text, code.@this code)
    {
        Text = text;
        Code = code;
    }

    /// <summary>A bare name — <c>%x%</c> — not a setting (<c>%!x%</c>) and not a way into one
    /// (<c>%x.y%</c>).</summary>
    internal bool IsBare => Code.Count == 1 && !Code.Root.Name.StartsWith('!');

    /// <summary>A value reached by members only — <c>%x%</c>, <c>%user.name%</c>.</summary>
    internal bool IsMembers => !Code.Root.Name.StartsWith('!')
        && Code.Items().Skip(1).All(h => h is code.Property { IsBinding: false });

    /// <summary>Reads one of the app's by its key — <c>%!app.goal["/show"]%</c>, <c>%!app.module["file"]%</c>: the
    /// app, a concept, then an index.</summary>
    internal bool IsKeyed => Code.Count > 2
        && string.Equals(Code.Root.Name, "!app", System.StringComparison.OrdinalIgnoreCase)
        && Code.Items().ElementAt(1) is code.Property { IsBinding: false }
        && Code.Items().ElementAt(2) is code.Index;

    /// <summary>The dotted paths a setting's variable spells, shortest first — <c>%!app.test.setting.parallel%</c>
    /// spells <c>app</c>, <c>app.test</c>, <c>app.test.setting</c>, <c>app.test.setting.parallel</c> (its root
    /// without the <c>!</c>, then each member). A binding, an index or a method ends them; a variable that is no
    /// setting's (<c>%x%</c>) spells none.</summary>
    internal IEnumerable<string> Paths
    {
        get
        {
            if (!Code.Root.Name.StartsWith('!')) yield break;
            var path = Code.Root.Name[1..];
            yield return path;
            foreach (var hop in Code.Items().Skip(1))
            {
                if (hop is not code.Property { IsBinding: false } member) yield break;
                path += "." + member.Name;
                yield return path;
            }
        }
    }

    /// <summary>The text between the % signs (<c>user.name</c>, <c>!data</c>) — the form the variable
    /// store takes while callers still hand it names.</summary>
    public string Name => Text.Length >= 2 ? Text[1..^1] : Text;

    /// <summary>Runs the code: what the variable holds.</summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this> Start(actor.context.@this context)
        => Code.Start(context);

    /// <summary>Writes <paramref name="value"/> where the variable names: every hop but the last
    /// reaches the parent, and the last writes itself.</summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this> Set(object? value, actor.context.@this context)
        => Code.Set(value, context);

    /// <summary>What the variable holds — or, when it holds nothing, the value <paramref name="value"/>
    /// gives birth to, written there (its code: a bare name in one step, a path read then written).</summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this> Ensure(
        System.Func<System.Threading.Tasks.ValueTask<global::app.data.@this>> value, actor.context.@this context)
        => Code.Ensure(value, context);

    // What the variable holds, its value touched (lazy content — a json list, a file's content — becomes what
    // it is), so a variable used as a list is its content; a failure or an ask is left as it is.
    private async System.Threading.Tasks.ValueTask<global::app.data.@this> Held(actor.context.@this context)
    {
        var held = await Start(context);
        if (held.Success && !held.Exits) await held.Value();
        return held;
    }

    /// <summary>What the variable holds, as a <typeparamref name="TAs"/>, handed to <paramref name="then"/> —
    /// its content, touched. A failure, an ask, or a value that isn't a TAs is the answer.</summary>
    public async System.Threading.Tasks.Task<global::app.data.@this> Use<TAs>(actor.context.@this context,
        System.Func<TAs, System.Threading.Tasks.Task<global::app.data.@this>> then)
        => await (await Held(context)).Use(then);

    /// <summary>What the variable holds, as a <typeparamref name="TAs"/>, changed in place by
    /// <paramref name="then"/>, then written back (<see cref="Replace"/>) — for a bare name holding that very
    /// instance there is nothing to write and nothing fires; a deeper variable (or a value that became a
    /// new instance when touched) is written, so what is bound on the set runs AFTER the change is made: a
    /// refusal there is the answer, but the change is already in the value. A failure, an ask, or a value
    /// that isn't a TAs is the answer, nothing changed.</summary>
    public async System.Threading.Tasks.Task<global::app.data.@this> Change<TAs>(actor.context.@this context,
        System.Func<TAs, System.Threading.Tasks.Task<global::app.data.@this>> then) where TAs : global::app.type.item.@this
    {
        var held = await Held(context);
        return await held.Use<TAs>(async value =>
        {
            var changed = await then(value);
            if (!changed.Success || changed.Exits) return changed;
            var kept = await Replace(held, value, context);
            return kept.Success && !kept.Handled ? changed : kept;
        });
    }

    /// <summary>Writes <paramref name="value"/> only if the variable still holds <paramref name="expected"/>
    /// — the Data the caller read — so a newer value written in between is left alone and is the answer;
    /// what is bound on the set answers too. A deeper variable's Data is born per read, so there it just
    /// writes.</summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this> Replace(global::app.data.@this expected,
        global::app.type.item.@this value, actor.context.@this context)
        => Code.Replace(expected, value, context);

    /// <summary>What the variable holds, through that value's own door (a container deep-renders, a
    /// template renders, a scalar answers itself). Loud: a variable that holds nothing throws — a
    /// referenced value that isn't there is a bug at the reference. Boolean questions (conditions)
    /// tolerate absence through their own path (condition.code.Default), never here. A chain of
    /// references deeper than the memory allows is a cycle.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        var resolving = data.Context.Variable.Resolving;
        if (resolving.Value++ > 50)
        {
            resolving.Value = 0;
            throw new global::app.error.AppException($"variable resolve cycle on '{Text}'", "VarResolveCycle", 400);
        }
        try
        {
            var resolved = await Start(data.Context);
            if (!resolved.IsInitialized)
                throw new global::app.error.VariableNotFoundException(Name);
            return await resolved.Value();
        }
        finally { resolving.Value--; }
    }

    /// <summary>
    /// The typed ask (<see cref="global::app.type.item.ICreate{TSelf}"/>): pass-through. A variable
    /// NAMES a thing — it is born from its text at the wire boundary, never converted from a value.
    /// Anything but a variable is a decline.
    /// </summary>
    public static @this? Create(global::app.type.item.@this value, global::app.data.@this data)
    {
        if (value is @this v) return v;
        data.Fail(new global::app.error.Error(
            $"%{data.Name}% holds a {value.Type.Name} — a variable names a thing; it is born typed (declare 'type:variable'), never created from a value.",
            "CreateVariableDeclined", 400));
        return null;
    }

    /// <summary>The name at a string-expecting boundary (<c>Variables.Get(name.Value)</c>).</summary>
    public static implicit operator string(@this v) => v.Name;

    /// <summary>
    /// variable's family <c>Convert</c> hook: the text a value holds, read as a variable
    /// (<see cref="parser.@this.Whole"/>); text that isn't one is a decline with the parser's reason.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind, actor.context.@this context)
    {
        var raw = value as string
            ?? (value as global::app.type.item.@this)?.Clr<string>()
            ?? value?.ToString() ?? "";
        var parser = new parser.@this(raw);
        return parser.Whole is { } born ? context.Ok(born) : context.Error(parser.Error[0]);
    }

    /// <summary>The raw-name callsites (<c>Data.As&lt;T&gt;</c>, the type's <c>Create</c> and
    /// <c>Read</c>, the variable reader) — throw boundaries: the variable <paramref name="raw"/> writes,
    /// or the parser's reason thrown. A <c>.pr</c> row's list (<paramref name="given"/>) already holds
    /// it, parsed at build.</summary>
    public static @this Resolve(string raw, actor.context.@this context, IReadOnlyList<@this>? given = null)
    {
        // a row's list holds it already — as written (%x%) or as its bare name (x)
        if (given?.FirstOrDefault(v => v.Text == raw || v.Name == raw) is { } held) return held;
        var parser = new parser.@this(raw);
        return parser.Whole ?? throw new global::app.error.AppException(parser.Error[0].Message, "InvalidVariable", 400);
    }

    /// <summary>A variable born from its text, for direct C# composition (tests, App.Run):
    /// <c>new variable("myList")</c> names <c>%myList%</c>.</summary>
    public @this(string name) : this(new parser.@this(name).Whole
                                     ?? throw new System.ArgumentException($"'{name}' is not a variable name.", nameof(name))) { }

    private @this(@this born) : this(born.Text, born.Code) { }

    public override string ToString() => Name;

    /// <summary>A variable is a leaf that NAMES a slot — its wire form is its text, which the
    /// parser reads back into the same variable.</summary>
    public override bool IsLeaf => true;

    /// <summary>A variable IS a reference (it resolves to what the binding holds). The instance-bind
    /// in Variable.Set reads this to alias the target.</summary>
    public override bool IsVariable => true;

    /// <summary>A variable holds itself.</summary>
    public override IReadOnlyList<@this> Variable => [this];

    /// <inheritdoc/>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this?> Get(actor.context.@this ctx)
        => await Start(ctx);

    /// <summary>One variable navigated as itself (<c>%!app.variable.user.name%</c>): its members, and its
    /// <c>type</c> — the type of what it holds.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
    {
        if (!string.Equals(key, "type", System.StringComparison.OrdinalIgnoreCase))
            return await base.Get(parent, key);
        var held = await Start(parent.Context);
        return held.IsInitialized ? new global::app.data.@this(key, held.Type, parent: parent) : held;
    }

    /// <summary>A reference renders itself FRESH every read — like a computed, never memoized onto
    /// the holding Data. The same authored reference (a goal-call param <c>planStep=%item%</c>) is
    /// reused across calls; caching one call's resolved value onto it would freeze every later call
    /// on the first binding.</summary>
    public override bool Cacheable => false;

    /// <summary>Its text, with its % signs — bare in formal (<c>Name: variable = %content%</c>), a
    /// string everywhere else.</summary>
    public override void Write(global::app.type.format.IWriter w)
    {
        if (w.Format == global::app.goal.step.action.formal.Writer.Token) w.Raw(Text);
        else w.String(Text);
    }
}
