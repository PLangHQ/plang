namespace app.type.item.text;

/// <summary>
/// PLang <c>text</c> type — the canonical name for textual content (replaces
/// the historical primitive <c>string</c> on the PLang surface; <c>string</c>
/// stays as an accepted alias). Mirrors <c>image</c> but text-backed instead
/// of bytes-backed.
///
/// <para>Kind is open (no advertised vocabulary): a <c>text</c> value's kind
/// comes from the file extension (<c>md</c>, <c>txt</c>, <c>csv</c>, …) via
/// the <see cref="Build"/> hook. A plain string with no extension is
/// <c>text</c> with kind <c>null</c>.</para>
///
/// <para>The wire shape is just a string — <see cref="Shape"/> says <c>string</c>.
/// The backing CLR <c>string</c> is the single source of truth; behavior
/// (length, case, contains, compare, truthiness) is a member on the wrapper, so
/// the <c>is string</c> consumer-switches collapse into method calls.</para>
///
/// <para><b>Case policy.</b> Order and value-equality are <em>ordinal,
/// case-insensitive</em>, in its own Order: <c>"abc" == "ABC"</c>, and they
/// sort together. <see cref="object.Equals(object)"/>/<see cref="GetHashCode"/>
/// align with that policy so a <c>text</c> works as a <c>HashSet</c> member /
/// dict key without surprise.</para>
///
/// <para><b>Atomicity.</b> <c>text</c> is a scalar, not a sequence of chars —
/// it deliberately does <b>not</b> implement <c>IEnumerable</c>, so
/// <c>foreach %s%</c> never char-iterates it.</para>
/// </summary>
[global::app.Attributes.PlangType("text")]
[global::app.Attributes.Format("", "text/plain", ".txt")]
[global::app.Attributes.Format("xml", "application/xml", Text = true)]
[global::app.Attributes.Format("md", "text/markdown", ".md", ".markdown")]
[global::app.Attributes.Format("yml", "text/yaml", ".yml", ".yaml")]
[global::app.Attributes.Format("ini", Text = true)]
[global::app.Attributes.Format("goal", Text = true)]
[global::app.Attributes.Format("llm", Text = true)]
[global::app.Attributes.Format("template", Text = true)]
[global::app.Attributes.Format("liquid", Text = true)]
// text's subclasses are its kinds (decision 783/788): a separator is a {text, kind: separator} — a text
// value with its own named values. Unsealed so such a value-subclass kind can derive from it.
[global::app.Attributes.Kinds]
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    System.IEquatable<@this>, global::app.type.item.IEncode<@this>
{
    /// <summary>
    /// Text's formats written: a text value is its characters, in the given encoding (UTF-8 when none) — no
    /// writer between; any other value writes itself as text (a leaf bare, a container as its json content).
    /// </summary>
    public static async System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View? view,
        System.Text.Encoding? encoding, System.Threading.CancellationToken ct)
    {
        var characters = encoding ?? System.Text.Encoding.UTF8;
        // a result that had failed before it was written out is shown — its error is what is written; a value that
        // fails here, as it is read (a %variable% not set), is the write's own failure, never written as the content
        var failed = !data.Success;
        var value = await data.Value();
        if (!failed && !data.Success) return data;
        if (value is @this text)
            await stream.WriteAsync(characters.GetBytes(text.ToString()), ct);
        else
            await data.Output(new global::app.type.item.text.Writer(stream, characters,
                context.Setting.Of<global::app.setting.@this>().Culture, view ?? global::app.View.Out, context));
        await stream.FlushAsync(ct);
        return context.Ok();
    }

    public static IReadOnlyList<string> Alias { get; } = ["string"];
    public static string Shape => "string";
    // No static Kinds — text's kind is open (derived from extension at build).

    // THE backing — a private field, not a property at any visibility.
    // Content leaves text only via Write(IWriter), the typed ops, or the
    // door; a .NET edge lowers through Clr.
    private readonly string _value;

    // The variables the template holds, each once — empty for plain text.
    private readonly IReadOnlyList<global::app.type.item.variable.@this> _variable = [];

    /// <inheritdoc/>
    public override IReadOnlyList<global::app.type.item.variable.@this> Variable => _variable;


    /// <summary>
    /// THE PURE CORE — "text, make yourself from this value, or decline." A <c>text</c> passes
    /// through; a scalar stringifies invariantly. A structured value (dict/list) renders its own
    /// canonical text through ITS output, not here — <c>text</c> knows nothing of those types.
    /// An opaque domain object has no honest textual form → <c>null</c> (decline). Born holding
    /// <paramref name="type"/>. Shared by the ICreate courier and comparison.
    /// </summary>
    public static @this? Create(object? raw, global::app.type.@this type)
    {
        if (raw is @this self) return self;
        // An item of another type unwraps to its clr (a read); a raw CLR value is already its clr.
        object? clr = raw is global::app.type.item.@this it ? it.Clr<object>() : raw;
        return clr switch
        {
            string s => new(s, type),
            System.IConvertible c => new(System.Convert.ToString(c, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, type),
            _ => null,
        };
    }

    /// <summary>The ICreate courier face — delegates to the pure core; a value with no textual form
    /// declines with the reason on <paramref name="data"/>. text's kind is a hint (extension), not a
    /// construction switch, so the core needs no kind.</summary>
    public static @this? Create(object? value, global::app.type.@this? declared, global::app.data.@this data)
    {
        // a text is held as it is when it already is the declared type, else born again holding it
        if (value is @this self) return declared is null || self.Is(declared) ? self : new @this(self, declared);
        if (Create(value, declared ?? data.Context.App.type.list[typeof(@this)]) is { } built) return built;
        data.Fail(new global::app.error.@this(
            $"Cannot bind a {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name)} to text — it has no textual form.", "TypeConversionFailed", 400));
        return null;
    }

    /// <summary>A stamped template's answer depends on outside state (%refs%
    /// can change between uses) — never kept. Plain text caches as always.</summary>
    public override bool Cacheable => Type.Template == null;

    /// <summary>
    /// THE door — ready means ready: a stamped template fills its holes
    /// against live variables at every use (never kept — see
    /// <see cref="Cacheable"/>). Full-match <c>%x%</c> answers with the
    /// variable's value through ITS own door (door recursion; the answer may
    /// be any type); partial (<c>"hello %name%"</c>) interpolates single-pass
    /// into fresh, unstamped text. An unset full-match ref is THIS type's own
    /// failure story — reported on the data binding, answer absent.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        if (Type.Template == null) return this;
        var context = data.Context;
        if (context?.Variable == null) return this;
        if (IsVariable)
        {
            var resolved = await _variable[0].Start(context);
            if (!resolved.IsInitialized)
            {
                data.Fail(new global::app.error.@this(
                    $"%{_variable[0].Name}% is not set — nothing to answer for {_value}.",
                    "VariableNotFound", 404));
                return data.Empty;
            }
            return await resolved.Value();
        }
        return new @this(await Rendered(context), Plain);
    }

    // The plain text type this text's own is of — what a text derived from it holds (rendered, upper-cased, cut): its
    // content, no template and no kind of this one's.
    private global::app.type.@this Plain => Type.Family;

    // The template rendered: one pass in order — each literal written as it is, each variable written
    // in place through its value's own door (its Output into a text writer: a wire materializes to
    // bare content, a container renders its json text). An unset variable is an error at the
    // reference; an unset %!x% (an optional engine internal) stays as written.
    private async System.Threading.Tasks.ValueTask<string> Rendered(global::app.actor.context.@this context)
    {
        using var ms = new System.IO.MemoryStream();
        var w = new global::app.type.item.text.Writer(ms, System.Text.Encoding.UTF8,
            context.Setting.Of<global::app.setting.@this>().Culture, global::app.View.Out, context);
        int pos = 0;
        for (int at = _value.IndexOf('%'); at >= 0; at = _value.IndexOf('%', at + 1))
        {
            var found = At(at);
            if (found == null) continue;
            w.String(_value[pos..at]);
            pos = at + found.Text.Length;
            var bound = await found.Start(context);
            if (!bound.IsInitialized)
            {
                if (found.Code.Root.Name.StartsWith('!')) { w.String(found.Text); at = pos - 1; continue; }
                throw await Unreachable(found, context);
            }
            // The crash net for this render's door (Start + Output, not the variable's Value door, which has its
            // own): a value reached again while it is being written into a template would recurse until the
            // process dies. Unreachable today — every binding a program writes is settled where it is written
            // (data.Settle: a set, an action's exit, a call's parameters, a loop's item), so nothing holds a
            // template that names itself — kept so a future binding that stores one fails as a plang error.
            var rendering = context.Variable.Rendering;
            var outer = rendering.Value ?? System.Collections.Immutable.ImmutableHashSet.Create<object>(
                System.Collections.Generic.ReferenceEqualityComparer.Instance);
            var value = bound.Peek();
            if (outer.Contains(value))
                throw new global::app.error.AppException($"variable resolve cycle: {found.Text} holds a template that renders {found.Text}", "VarResolveCycle", 400);
            rendering.Value = outer.Add(value);
            try { await bound.Output(w); }
            finally { rendering.Value = outer; }
            at = pos - 1;
        }
        w.String(_value[pos..]);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    // The variable written at `at`, the longest when one's text starts another's.
    private global::app.type.item.variable.@this? At(int at)
        => _variable.Where(v => string.CompareOrdinal(_value, at, v.Text, 0, v.Text.Length) == 0)
                    .MaxBy(v => v.Text.Length);

    // Why a variable didn't resolve: how far its hops reached, and the hop that answered nothing.
    // A bare root gets the plain "not set" form.
    private async System.Threading.Tasks.ValueTask<global::app.error.VariableNotFoundException> Unreachable(
        global::app.type.item.variable.@this variable, global::app.actor.context.@this context)
    {
        var hops = variable.Code.Items().ToList();
        if (hops.Count <= 1) return new global::app.error.VariableNotFoundException(variable.Name);
        string reachedPrefix = "(root)", reachedType = "nothing", reached = "";
        global::app.data.@this? current = null;
        foreach (var hop in hops)
        {
            current = await hop.Start(current, context);
            if (!current.IsInitialized)
                return new global::app.error.VariableNotFoundException(variable.Name, reachedPrefix, reachedType, hop.Text.TrimStart('.'));
            reached += hop.Text;
            reachedPrefix = reached;
            reachedType = current.Type.Name;
        }
        return new global::app.error.VariableNotFoundException(variable.Name);
    }

    /// <summary>
    /// The text writes ITSELF, as written: its characters, with the kind they are in (a template's
    /// <c>%ref%</c> as the step gave it). Whether a template is kept as written or used (resolved, rendered)
    /// is the writer's view's to decide, where its Data is written; using it is its <see cref="Value"/>.
    /// </summary>
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.Content(_value, Format(writer.Context));
        return default;
    }

    // The kind these characters are in, as the types hold it — one of text's own (md) or one text coins for another
    // type's format (json); with no context to ask, the kind by its name alone.
    private global::app.type.kind.@this Format(global::app.actor.context.@this? context)
        => context?.App.type.list.Kind(Type, context) ?? Type.kind;

    /// <summary>A text holds what a step quoted by its own characters, whatever kind they are in: a json text
    /// <c>{"size": 10}</c> holds the step's <c>{"size": 10}</c>, never only the escaped form a writer would give it.</summary>
    internal override System.Threading.Tasks.ValueTask<bool> Holds(string quoted, global::app.actor.context.@this context)
        => System.Threading.Tasks.ValueTask.FromResult(_value.Contains(quoted));

    public override bool IsLeaf => true;

    /// <summary>A stamped template whose WHOLE content is one <c>%ref%</c> IS a reference
    /// (it resolves to the named binding, not renders). A partial template
    /// (<c>"hello %name%"</c>) is content — it renders, so it is NOT a variable.</summary>
    public override bool IsVariable
        => Type.Template != null && _variable is [var only] && only.Text == _value;

    /// <inheritdoc/>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this?> Get(actor.context.@this ctx)
        => IsVariable ? await _variable[0].Start(ctx) : null;

    // What the characters stand for, as their kind opens them (a text of kind json: the json value) — opened at the
    // first navigation, not before, and kept; the text stays the text it is. Only what opened is kept: it holds no
    // context, so every asker navigates it with their own.
    private global::app.type.item.@this? _opened;

    // The characters opened by their kind, for this asker: what was kept, else opened now (and kept when it opened);
    // a failed Data when they don't read as their kind says; null when the kind opens nothing.
    private global::app.data.@this? Opened(global::app.actor.context.@this? context)
    {
        if (_opened is { } kept) return new global::app.data.@this("", kept, context: context);
        var opened = Format(context).Open(_value, context!);
        if (opened is { Success: true }) _opened = opened.Peek();
        return opened;
    }

    /// <summary>
    /// A text navigates as its kind opens its characters: a text of kind json (<c>%x.a%</c>) is walked as the json
    /// its characters are, parsed only then, and stays text; characters that don't read as json answer why
    /// (MaterializeFailed). A text whose kind opens nothing has no by-key structure — navigating it
    /// (<c>%x.port%</c>) is an authoring error. Method calls (<c>%x.grep("..")%</c>) are text's own methods, not
    /// this, so they are unaffected.
    /// </summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key)
        => Get(parent, key, isIndex: false);

    /// <summary>A text iterates as its kind opens its characters — a text of kind json holding an array yields its
    /// elements; any other text is one value, yielded once, never its characters. Characters that don't read as their
    /// kind stop the loop with why.</summary>
    public override System.Collections.Generic.IEnumerable<(global::app.data.@this key, global::app.data.@this value)>
        EnumerateItems(global::app.actor.context.@this? context)
        => Opened(context) is { } opened
            ? opened.Success ? opened.Peek().EnumerateItems(context) : throw new global::app.error.AppException(opened.Error!)
            : base.EnumerateItems(context);

    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key, bool isIndex)
    {
        // its own members first (%s.length%) — the ones it shows plang
        if (!isIndex && GetType().GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.IgnoreCase) is { } member && System.Attribute.IsDefined(member, typeof(global::app.LlmBuilderAttribute)))
            return System.Threading.Tasks.ValueTask.FromResult(new global::app.data.@this(key, member.GetValue(this), parent: parent));
        if (Opened(parent.Context) is { } opened)
            return opened.Success ? opened.Peek().Get(parent, key, isIndex) : System.Threading.Tasks.ValueTask.FromResult(opened);
        var who = string.IsNullOrEmpty(parent.Name) ? "value" : $"%{parent.Name}%";
        var err = parent.Context.Error(new global::app.error.@this(
            $"cannot navigate .{key}: {who} is text", "CantNavigateText", 400));
        err.Name = key;
        return System.Threading.Tasks.ValueTask.FromResult(err);
    }

    /// <summary>
    /// Born holding <paramref name="type"/> — the app's, handed in by its maker: the row's type (<c>text&lt;md&gt;</c>, a
    /// template text) or plain <c>text</c>. The build decided template-ness and the .pr carries it on the type; the
    /// run never re-parses on load.
    /// <para><paramref name="variable"/> is the row's own variable list (from the <c>.pr</c>); the text keeps the
    /// ones its content holds, matched by name. A template made without a .pr list (built, or at run) parses its
    /// content for them, as before. A non-template text holds none.</para>
    /// </summary>
    public @this(string value, global::app.type.@this type, IReadOnlyList<global::app.type.item.variable.@this>? variable = null)
        : base(type)
    {
        _value = value ?? string.Empty;
        _variable = Type.Template == null ? []
            : variable is { } rows ? rows.Where(v => _value.Contains(v.Text, System.StringComparison.Ordinal)).DistinctBy(v => v.Text).ToList()
            : new global::app.type.item.variable.parser.@this(_value, Type.Types ?? throw new System.InvalidOperationException(
                "a text template is born holding the app's type — a bare one holds no types to read its variables with")).Variable;
    }

    /// <summary>The empty text — what a text holds before anything is in it (type.Empty hands its type).</summary>
    internal @this(global::app.type.@this type) : this(string.Empty, type) { }

    // The string → text operator's birth: no app to ask, so it holds a type built from the class — until the
    // implicit-operator branch.
    private @this(string value) : this(value, new global::app.type.@this("text", typeof(@this))) { }

    // The same content and variables, born holding the declared type.
    private @this(@this source, global::app.type.@this type) : base(type)
    {
        _value = source._value;
        _variable = source._variable;
    }

    /// <summary>
    /// Construction from a raw form — a string is the value as-is; binary bytes
    /// off I/O decode as UTF-8. Text is only ever born from a string, so this is
    /// the one place bytes become that string (a reader handed raw stream bytes
    /// reaches the text here). <paramref name="type"/> and <paramref name="variable"/> as above.
    /// </summary>
    public @this(object raw, global::app.type.@this type, IReadOnlyList<global::app.type.item.variable.@this>? variable = null)
        : this(raw switch
        {
            byte[] b => System.Text.Encoding.UTF8.GetString(b),
            string s => s,
            _ => raw?.ToString() ?? string.Empty,
        }, type, variable)
    { }

    // INBOUND only — the entry lift (`.Ok("x")` constructs). The outbound
    // implicit (text → string) is gone: every site was a silent CLR exit;
    // a reader names the string face (`.Value`) at a real .NET edge.
    public static implicit operator @this(string s) => new(s);

    // Only the @this==@this overload — NOT a string overload. string is a reference
    // type, so a string overload would make `text == null` ambiguous (null fits both).
    // `text == "literal"` is written via the typed ops (Contains/AreEqual) or ToString at a display edge.
    public static bool operator ==(@this? a, @this? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(@this? a, @this? b) => !(a == b);

    public override string ToString() => _value;

    /// <summary>A text's bytes are its UTF-8.</summary>
    public override async System.Threading.Tasks.Task<global::app.data.@this> Pour(System.IO.Stream into, actor.context.@this context)
    {
        await into.WriteAsync(System.Text.Encoding.UTF8.GetBytes(_value));
        return context.Ok();
    }

    /// <summary>A diagnostic shows text quoted, apart from a number or a name.</summary>
    public override System.Threading.Tasks.ValueTask<string> Debug(global::app.actor.context.@this context) => new($"\"{_value}\"");

    /// <summary>The CLR exit door — text hands its own backing string; the
    /// shared converter (strict, loud on junk) carries it to the target.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(_value, target);

    // ---- Ops (the behavioral targets of the `is string` sweep) ----

    /// <summary>Codepoint (Unicode scalar) count — surrogate pairs count once.
    /// Returns the PLang <c>number</c> (the public surface answers in PLang values).</summary>
    [LlmBuilder] public global::app.type.item.number.@this Length
    {
        get
        {
            int count = 0;
            foreach (var _ in _value.EnumerateRunes()) count++;
            return count;
        }
    }

    // ---- Methods a variable reaches: %name.toupper()%, %text.replace("-", " ")% ----

    [LlmBuilder] public @this ToUpper() => new(_value.ToUpperInvariant(), Plain);
    [LlmBuilder] public @this ToLower() => new(_value.ToLowerInvariant(), Plain);
    [LlmBuilder] public @this Trim() => new(_value.Trim(), Plain);

    /// <summary>Every <paramref name="old"/> replaced by <paramref name="new"/>.</summary>
    [LlmBuilder] public @this Replace(@this old, @this @new) => new(_value.Replace(old._value, @new._value), Plain);

    /// <summary>At most <paramref name="max"/> characters, "..." marking a cut; 0 is no limit.</summary>
    [LlmBuilder]
    public @this MaxLength(global::app.type.item.number.@this max)
    {
        var limit = max.Clr<int>();
        return limit <= 0 || _value.Length <= limit ? this : new(_value[..limit] + "...", Plain);
    }

    /// <summary>The lines matching <paramref name="pattern"/> — each <c>line number: line</c> — with <paramref name="lines"/>
    /// lines around each when asked (0, none, by default), through the grep the app registered (<c>app.Code</c>), else
    /// the default line matcher.</summary>
    [LlmBuilder]
    public async System.Threading.Tasks.Task<global::app.data.@this<global::app.type.item.list.@this<@this>>> Grep(@this pattern,
        global::app.actor.context.@this context, global::app.type.item.number.@this? lines = null)
    {
        var found = (context.App.Code.Get<global::app.data.code.IGrep>().Provider ?? new global::app.data.code.Default())
            .Grep(new global::app.data.@this("", this, context: context), pattern._value, lines?.Clr<int>() ?? 0);
        if (!found.Success) return global::app.data.@this<global::app.type.item.list.@this<@this>>.From(found);
        var text = (await found.Value())?.ToString() ?? "";
        return context.Ok(new global::app.type.item.list.@this<@this>(
            text.Length == 0 ? [] : [.. text.Split('\n').Select(line => (global::app.type.item.@this)new @this(line.TrimEnd('\r'), Plain))],
            context.App.type.list[typeof(global::app.type.item.list.@this<@this>)]));
    }

    /// <summary>How many lines match <paramref name="pattern"/>.</summary>
    [LlmBuilder]
    public async System.Threading.Tasks.Task<global::app.data.@this<global::app.type.item.number.@this>> GrepCount(@this pattern,
        global::app.actor.context.@this context)
    {
        var counted = (context.App.Code.Get<global::app.data.code.IGrep>().Provider ?? new global::app.data.code.Default())
            .GrepCount(new global::app.data.@this("", this, context: context), pattern._value);
        return counted.Success ? counted.As<global::app.type.item.number.@this>() : global::app.data.@this<global::app.type.item.number.@this>.From(counted);
    }

    /// <summary>The item membership hook — substring, same policy as below. The needle is read
    /// through its value door: a literal operand can still be lazy, and its ToString() (Peek)
    /// would then read "(null)", making every contains false.</summary>
    public override async System.Threading.Tasks.ValueTask<bool> Contains(global::app.data.@this needle)
        => Contains((await needle.Value())?.ToString());

    /// <summary>text's raw string face — its characters.</summary>
    public override string? RawText => _value;

    /// <summary>text's byte face — its UTF-8 content (used when encoding a text to base64).</summary>
    public override byte[]? RawBytes => System.Text.Encoding.UTF8.GetBytes(_value);

    public bool Contains(string other) =>
        _value.Contains(other ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    public bool StartsWith(string other) =>
        _value.StartsWith(other ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    public bool EndsWith(string other) =>
        _value.EndsWith(other ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    public int IndexOf(string other) =>
        _value.IndexOf(other ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);

    public @this Substring(int start, int length) => new(_value.Substring(start, length), Plain);
    public @this Replace(string oldValue, string newValue) =>
        new(_value.Replace(oldValue ?? string.Empty, newValue ?? string.Empty), Plain);

    // ---- Truthiness (item) ----

    /// <summary>Empty text is falsy; any non-empty text is truthy.</summary>
    public override bool IsTruthy() => _value.Length > 0;

    // ---- Comparison — the value's own behavior (see app.data.Comparison) ----

    /// <summary>Specificity floor — every other ranked type outranks text and drives.</summary>
    public override int Rank => 100;

    /// <summary>
    /// Ordinal, case-insensitive ordering in caller order. Text is the floor type,
    /// so it only drives a pair the other side couldn't claim (text vs text). The other
    /// side coerces into text through the pure <c>Create</c> core — the wrapper's content,
    /// a raw string, an enum's NAME, or a domain value's canonical text form; a container
    /// has no honest text form so <c>%dict% == "text"</c> is Incomparable.
    /// </summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other, global::app.actor.context.@this context)
    {
        var b = other as @this ?? Create(other, context.App.type.list[typeof(@this)]);
        if (b is null) return new(global::app.data.Comparison.Incomparable);
        var c = string.Compare(_value, b._value, System.StringComparison.OrdinalIgnoreCase);
        return new(c < 0 ? global::app.data.Comparison.Less
                 : c > 0 ? global::app.data.Comparison.Greater
                 : global::app.data.Comparison.Equal);
    }

    // ---- Equality + order (ordinal, case-insensitive — see class doc) ----

    public bool AreEqual(object? other) => other switch
    {
        @this t => string.Equals(_value, t._value, System.StringComparison.OrdinalIgnoreCase),
        string s => string.Equals(_value, s, System.StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public bool Equals(@this? other) =>
        other is not null && string.Equals(_value, other._value, System.StringComparison.OrdinalIgnoreCase);
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => System.StringComparer.OrdinalIgnoreCase.GetHashCode(_value);
}
