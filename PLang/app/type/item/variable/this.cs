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
[global::app.Attributes.PlangType]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, IName
{
    public static string Example => "%user%";
    public static string Description => "A variable, named between % signs, that holds a value.";

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
    /// makes, written there. A bare name does it in one step (runs asking at once all answer the same
    /// Data); a deeper variable reads, then writes.</summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.@this> Ensure(
        System.Func<global::app.type.item.@this> value, actor.context.@this context)
    {
        if (Code.Count == 1) return await context.Variable.Ensure(Code.Root.Name, value);
        var held = await Start(context);
        if (held.IsInitialized) return held;
        await Set(value(), context);
        return await Start(context);
    }

    /// <summary>Writes <paramref name="value"/> only if the variable still holds <paramref name="expected"/>
    /// — the Data the caller read — so a newer value written in between is left alone; answers whether
    /// it now holds the value. A deeper variable's Data is born per read, so there it just writes.</summary>
    public async System.Threading.Tasks.ValueTask<bool> Replace(global::app.data.@this expected,
        global::app.type.item.@this value, actor.context.@this context)
    {
        if (Code.Count == 1) return await context.Variable.Replace(Code.Root.Name, expected, value);
        return (await Set(value, context)).Success;
    }

    private static readonly System.Threading.AsyncLocal<int> _resolveDepth = new();

    /// <summary>What the variable holds, through that value's own door (a container deep-renders, a
    /// template renders, a scalar answers itself). Loud: a variable that holds nothing throws — a
    /// referenced value that isn't there is a bug at the reference. Boolean questions (conditions)
    /// tolerate absence through their own path (condition.code.Default), never here.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        if (_resolveDepth.Value++ > 50)
        {
            _resolveDepth.Value = 0;
            throw new global::app.error.AppException($"variable resolve cycle on '{Text}'", "VarResolveCycle", 500);
        }
        try
        {
            var resolved = await Start(data.Context);
            if (!resolved.IsInitialized)
                throw new global::app.error.VariableNotFoundException(Name);
            return await resolved.Value();
        }
        finally { _resolveDepth.Value--; }
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
    /// variable's family <c>Convert</c> hook: a variable is born from its text — <c>%x%</c>, or the
    /// bare name <c>x</c> a name slot may carry — through the parser. Text that isn't a variable is a
    /// decline with the parser's reason.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind, actor.context.@this context)
    {
        var raw = value as string
            ?? (value as global::app.type.item.@this)?.Clr<string>()
            ?? value?.ToString() ?? "";
        var text = raw.StartsWith('%') ? raw : "%" + raw + "%";
        var parser = new parser.@this(text);
        if (parser.Read(0) is { } born && born.Text.Length == text.Length) return context.Ok(born);
        return context.Error(parser.Error.FirstOrDefault()
            ?? new global::app.error.Error($"'{raw}' is not a variable.", "InvalidVariable", 400));
    }

    /// <summary>The raw-name callsites (<c>Data.As&lt;T&gt;</c>, the type's <c>Create</c> and
    /// <c>Read</c>, the variable reader) — throw boundaries: the variable <paramref name="raw"/> writes,
    /// or the parser's reason thrown. A <c>.pr</c> row's list (<paramref name="given"/>) already holds
    /// it, parsed at build.</summary>
    public static @this Resolve(string raw, actor.context.@this context, IReadOnlyList<@this>? given = null)
    {
        var text = raw.StartsWith('%') ? raw : "%" + raw + "%";
        if (given?.FirstOrDefault(v => v.Text == text) is { } held) return held;
        var born = Convert(raw, null, context);
        return born.Peek() as @this
               ?? throw new global::app.error.AppException(born.Error?.Message ?? $"'{raw}' is not a variable.", "InvalidVariable", 400);
    }

    /// <summary>A variable born from its text, for direct C# composition (tests, App.Run):
    /// <c>new variable("myList")</c> names <c>%myList%</c>.</summary>
    public @this(string name) : this(Born(name)) { }

    private @this(@this born) : this(born.Text, born.Code) { }

    private static @this Born(string name)
        => new parser.@this("%" + name + "%").Read(0)
           ?? throw new System.ArgumentException($"'{name}' is not a variable name.", nameof(name));

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

    /// <summary>A reference renders itself FRESH every read — like a computed, never memoized onto
    /// the holding Data. The same authored reference (a goal-call param <c>planStep=%item%</c>) is
    /// reused across calls; caching one call's resolved value onto it would freeze every later call
    /// on the first binding.</summary>
    public override bool Cacheable => false;

    /// <summary>Its text, with its % signs — bare in formal (<c>Name: variable = %content%</c>), a
    /// string everywhere else.</summary>
    public override void Write(global::app.channel.serializer.IWriter w)
    {
        if (w.Format == global::app.channel.serializer.formal.Writer.Token) w.Raw(Text);
        else w.String(Text);
    }
}
