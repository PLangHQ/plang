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
/// case-insensitive</em> — matching the historical <c>ScalarComparer</c> string
/// behavior so <c>"abc" == "ABC"</c> and sort order are unchanged after text
/// flows native. <see cref="object.Equals(object)"/>/<see cref="GetHashCode"/>
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
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
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
                context.Setting.Of<global::app.setting.@this>().Culture), view ?? global::app.View.Out, context);
        await stream.FlushAsync(ct);
        return context.Ok();
    }

    public static string Example => "Hello, world";
    public static IReadOnlyList<string> Alias { get; } = ["string"];
    public static string Shape => "string";
    /// <summary>
    /// LLM-facing teaching: text's kind comes from the file extension
    /// (`md`, `txt`, `csv`, `html`, …). The kind is a hint by default; strict
    /// is a no-op for text since plain vs markdown can't be probed from content.
    /// </summary>
    public static string Description =>
        "Textual content. Kind is set from the file extension (md, txt, csv, html, ...). "
        + "Kind is a hint by default; strict is a no-op for text (plain vs markdown is "
        + "not detectable from content).";
    // No static Kinds — text's kind is open (derived from extension at build).

    // THE backing — a private field, not a property at any visibility.
    // Content leaves text only via Write(IWriter), the typed ops, or the
    // door; a .NET edge lowers through Clr.
    private readonly string _value;

    // The variables the template holds, each once — empty for plain text.
    private readonly IReadOnlyList<global::app.type.item.variable.@this> _variable = [];

    /// <inheritdoc/>
    public override IReadOnlyList<global::app.type.item.variable.@this> Variable => _variable;

    /// <summary>The value's kind — the file-extension vocabulary (md, csv, …).
    /// An ordinary typed property stamped at creation, never after.</summary>
    public string? Kind { get; init; }

    // The text's type, made once, when it is first asked for (a text's kind and template are stamped at creation, so
    // it never changes after). A text writes itself by asking its type's kind, so it holds its type, never
    // rebuilding it on every read.
    private global::app.type.@this? _type;

    protected internal override global::app.type.@this Type
        => _type ??= new("text", typeof(@this), Kind, template: Template);

    /// <summary>
    /// THE PURE CORE — "text, make yourself from this value, or decline." A <c>text</c> passes
    /// through; a scalar stringifies invariantly. A structured value (dict/list) renders its own
    /// canonical text through ITS output, not here — <c>text</c> knows nothing of those types.
    /// An opaque domain object has no honest textual form → <c>null</c> (decline). Shared by the
    /// ICreate courier and comparison.
    /// </summary>
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        // An item of another type unwraps to its clr (a read); a raw CLR value is already its clr.
        object? clr = raw is global::app.type.item.@this it ? it.Clr<object>() : raw;
        return clr switch
        {
            string s => (@this)s,
            System.IConvertible c => (@this)(System.Convert.ToString(c, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty),
            _ => null,
        };
    }

    /// <summary>The ICreate courier face — delegates to the pure core; a value with no textual form
    /// declines with the reason on <paramref name="data"/>. text's kind is a hint (extension), not a
    /// construction switch, so the core needs no kind.</summary>
    public static @this? Create(object? value, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (Create(value) is { } built) return built;
        data.Fail(new global::app.error.Error(
            $"Cannot bind a {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name)} to text — it has no textual form.", "TypeConversionFailed", 400));
        return null;
    }

    /// <summary>A stamped template's answer depends on outside state (%refs%
    /// can change between uses) — never kept. Plain text caches as always.</summary>
    public override bool Cacheable => Template == null;

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
        if (Template == null) return this;
        var context = data.Context;
        if (context?.Variable == null) return this;
        if (IsVariable)
        {
            var resolved = await _variable[0].Start(context);
            if (!resolved.IsInitialized)
            {
                data.Fail(new global::app.error.Error(
                    $"%{_variable[0].Name}% is not set — nothing to answer for {_value}.",
                    "VariableNotFound", 404));
                return Absent;
            }
            return await resolved.Value();
        }
        return new @this(await Rendered(context));
    }

    // The template rendered: one pass in order — each literal written as it is, each variable written
    // in place through its value's own door (its Output into a text writer: a wire materializes to
    // bare content, a container renders its json text). An unset variable is an error at the
    // reference; an unset %!x% (an optional engine internal) stays as written.
    private async System.Threading.Tasks.ValueTask<string> Rendered(global::app.actor.context.@this context)
    {
        using var ms = new System.IO.MemoryStream();
        var w = new global::app.type.item.text.Writer(ms, System.Text.Encoding.UTF8,
            context.Setting.Of<global::app.setting.@this>().Culture);
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
            try { await bound.Output(w, global::app.View.Out, context); }
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
    /// The text writes ITSELF (OBP: the value is responsible for its own render), so
    /// <c>data.Output</c> never pre-materialises through <c>Value</c>. Plain content and the
    /// Store view write the raw form verbatim (a <c>.pr</c> keeps the authored <c>%ref%</c>). A
    /// whole-match <c>%ref%</c> is a reference — the bound value writes itself. A partial template
    /// renders to its string form (a template becomes ONE value — its literals+refs can't ride as
    /// interleaved tokens into a json/plang writer) and writes that. Either way the text hands the writer its
    /// characters with the kind they are in; the writer decides how they look.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask Output(
        global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        if (Template == null || mode == global::app.View.Store || context?.Variable == null)
        {
            writer.Content(_value, Format(context));
            return;
        }
        if (IsVariable)
        {
            var resolved = await Get(context);
            if (resolved is { IsInitialized: true }) { await resolved.Output(writer, mode, context); return; }
            throw new global::app.error.VariableNotFoundException(resolved!.Name);   // the lookup already carries the name
        }
        writer.Content(await Rendered(context), Format(context));
    }

    // The kind these characters are in, as the types hold it — one of text's own (md) or one text coins for another
    // type's format (json); with no context to ask, the kind by its name alone.
    private global::app.type.kind.@this Format(global::app.actor.context.@this? context)
        => context?.App.type.list.Kind(Type, context) ?? Type.kind;

    public override bool IsLeaf => true;

    /// <summary>A stamped template whose WHOLE content is one <c>%ref%</c> IS a reference
    /// (it resolves to the named binding, not renders). A partial template
    /// (<c>"hello %name%"</c>) is content — it renders, so it is NOT a variable.</summary>
    public override bool IsVariable
        => Template != null && _variable is [var only] && only.Text == _value;

    /// <inheritdoc/>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this?> Get(actor.context.@this ctx)
        => IsVariable ? await _variable[0].Start(ctx) : null;

    public override void Write(global::app.type.format.IWriter w) => w.String(_value);

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
        var err = parent.Context.Error(new global::app.error.Error(
            $"cannot navigate .{key}: {who} is text", "CantNavigateText", 400));
        err.Name = key;
        return System.Threading.Tasks.ValueTask.FromResult(err);
    }

    public @this(string value) { _value = value ?? string.Empty; }

    /// <summary>The empty text — what a text holds before anything is in it (type.Empty).</summary>
    internal @this() : this(string.Empty) { }

    /// <summary>
    /// Construction with a template mode. <paramref name="template"/> is the
    /// authored-content mode the reader carries — <c>"plang"</c> when the bytes are
    /// developer-authored (a goal/<c>.pr</c>), null for runtime-ingest. Text decides
    /// for itself whether there is actually a template to stamp: only a value holding a
    /// variable keeps the mode, so a plain string never reports as a variable reference.
    /// Resolution stays lazy — nothing renders until the door (<see cref="Value"/>). The
    /// trust is the mode, not the content: a structural text (a dict key, a type name) or a
    /// runtime-ingest value is born with a null mode and prints literally.
    /// <para><paramref name="variable"/> is what the <c>.pr</c> row says its value holds (the
    /// ones written in this text are taken); without it — a template born at build or at run —
    /// the text is parsed.</para>
    /// </summary>
    public @this(string value, global::app.type.item.template.kind.@this? template, IReadOnlyList<global::app.type.item.variable.@this>? variable = null)
    {
        _value = value ?? string.Empty;
        // Container inner slots (list/dict entries) have no per-slot .pr flag; the authored
        // read mode + a variable in the content marks them. Flagging literal slots would change
        // their canonicalization/signing.
        if (template == null || !_value.Contains('%')) return;
        var held = variable?.Where(v => _value.Contains(v.Text, System.StringComparison.Ordinal)).ToList()
                   ?? (IReadOnlyList<global::app.type.item.variable.@this>)new global::app.type.item.variable.parser.@this(_value).Variable;
        if (held.Count == 0) return;
        _variable = held.DistinctBy(v => v.Text).ToList();
        Template = template;
    }

    // A re-kinded copy: the same content and variables, the declared kind stamped.
    private @this(@this source, string? kind)
    {
        _value = source._value;
        _variable = source._variable;
        Template = source.Template;
        Kind = kind;
    }

    /// <summary>
    /// Construction from a raw form — a string is the value as-is; binary bytes
    /// off I/O decode as UTF-8. Text is only ever born from a string, so this is
    /// the one place bytes become that string (a reader handed raw stream bytes
    /// reaches the text here). <paramref name="template"/> and <paramref name="variable"/> as above.
    /// </summary>
    public @this(object raw, global::app.type.item.template.kind.@this? template = null, IReadOnlyList<global::app.type.item.variable.@this>? variable = null)
        : this(raw switch
        {
            byte[] b => System.Text.Encoding.UTF8.GetString(b),
            string s => s,
            _ => raw?.ToString() ?? string.Empty,
        }, template, variable)
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

    [LlmBuilder] public @this ToUpper() => new(_value.ToUpperInvariant());
    [LlmBuilder] public @this ToLower() => new(_value.ToLowerInvariant());
    [LlmBuilder] public @this Trim() => new(_value.Trim());

    /// <summary>Every <paramref name="old"/> replaced by <paramref name="new"/>.</summary>
    [LlmBuilder] public @this Replace(@this old, @this @new) => new(_value.Replace(old._value, @new._value));

    /// <summary>At most <paramref name="max"/> characters, "..." marking a cut; 0 is no limit.</summary>
    [LlmBuilder]
    public @this MaxLength(global::app.type.item.number.@this max)
    {
        var limit = max.Clr<int>();
        return limit <= 0 || _value.Length <= limit ? this : new(_value[..limit] + "...");
    }

    /// <summary>The lines matching <paramref name="pattern"/>, through the grep provider.</summary>
    [LlmBuilder]
    public global::app.data.@this Grep(@this pattern, global::app.actor.context.@this context)
        => Grep(pattern, 0, context);

    /// <summary>The lines matching <paramref name="pattern"/> with <paramref name="lines"/> lines around
    /// each, through the grep the app registered (<c>app.Code</c>), else the default line matcher.</summary>
    [LlmBuilder]
    public global::app.data.@this Grep(@this pattern, global::app.type.item.number.@this lines, global::app.actor.context.@this context)
        => (context.App.Code.Get<global::app.data.code.IGrep>().Provider ?? new global::app.data.code.Default())
            .Grep(new global::app.data.@this("", this, context: context), pattern._value, lines.Clr<int>());

    /// <summary>How many lines match <paramref name="pattern"/>.</summary>
    [LlmBuilder]
    public global::app.data.@this GrepCount(@this pattern, global::app.actor.context.@this context)
        => (context.App.Code.Get<global::app.data.code.IGrep>().Provider ?? new global::app.data.code.Default())
            .GrepCount(new global::app.data.@this("", this, context: context), pattern._value);

    /// <summary>The item membership hook — substring, same policy as below.</summary>
    public override System.Threading.Tasks.ValueTask<bool> Contains(global::app.data.@this needle)
        => System.Threading.Tasks.ValueTask.FromResult(Contains(needle.ToString()));

    /// <summary>A re-kinded copy — same content, the declared kind stamped
    /// (values immutable, never restamped in place).</summary>
    public override global::app.type.item.@this Kinded(string? kind) => new @this(this, kind);

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

    public @this Substring(int start, int length) => new(_value.Substring(start, length));
    public @this Replace(string oldValue, string newValue) =>
        new(_value.Replace(oldValue ?? string.Empty, newValue ?? string.Empty));

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
        var b = other as @this ?? Create(other);
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
