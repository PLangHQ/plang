using System.Text;

namespace app.goal.step.action.formal;

/// <summary>
/// Reads a step's actions from formal — <c>file.read(Path="notes.txt"); variable.set(Name=%content%, Value=%!data%)</c>
/// — the read-side twin of the formal writer (<see cref="Writer"/>). Born with the step whose actions it reads,
/// as <see cref="serializer.Reader"/> is, so every action it makes holds that step.
///
/// <para>Grammar (whitespace, new lines included, between any two tokens):</para>
/// <code>
///   actions = action { ";" action }
///   action  = module "." name "(" [ row { "," row } ] ")" [ "{" actions "}" ]
///   row     = Name [ ":" type ] ( "=" | "?=" ) value          ?= : a frozen default (the action's Default)
///   value   = "text" | number | true | false | null | %variable% | [ … ] | { … } | action
/// </code>
/// <para><c>{ }</c> is only a condition's body (its child step). A clause follows the action it is a clause
/// of, as its sibling in the same list — <c>file.read(…); on.error(…); on.cache(…)</c> — in the order written.
/// on.error's recovery is its <c>Recovery=[…]</c>, a list of actions.</para>
///
/// <para>A value's type never comes from the text alone: it is the catalogue's declared type for the
/// property, or — in an open <c>item</c> slot — the written type if any, else the literal's own. A written
/// type against a declared one must equal it. A choice's option may stand bare; a single unquoted word in a
/// text property is that text; a variable slot given a bare name reads it as the variable. A dict handed to
/// a list property is its argument rows (<c>Parameter={to: %x%}</c>). Each value is born through the type's
/// own door (<see cref="app.type.@this.Read"/>), the one a <c>.pr</c> row's value reads through.</para>
///
/// <para>A formal step is the programmer's or the LLM's writing, so a problem is an error, not an exception:
/// <see cref="Read"/> answers the actions, or a <c>FormalInvalid</c> error naming the line, the column and
/// the fix.</para>
/// </summary>
public sealed class Reader
{
    private readonly global::app.goal.step.@this _step;
    private readonly global::app.type.item.list.@this<global::app.module.@this> _modules;

    /// <summary>Born with the step it reads for and the modules its actions are picked from
    /// (<c>app.module.list</c>).</summary>
    public Reader(global::app.goal.step.@this step, global::app.type.item.list.@this<global::app.module.@this> modules)
    {
        _step = step;
        _modules = modules;
    }

    /// <summary>The step's actions read from <paramref name="text"/> — Ok(action.list), or the error.</summary>
    public async System.Threading.Tasks.Task<global::app.data.@this> Read(string text, global::app.actor.context.@this context)
    {
        try
        {
            var cursor = new Cursor(text, _step, context, _step.Index, _modules);
            cursor.Space();
            if (cursor.AtEnd) cursor.Fail("a step holds at least one action");
            var list = new global::app.goal.step.action.list.@this(context.App.type.list[typeof(global::app.goal.step.action.list.@this)]);
            foreach (var action in await cursor.Actions(null)) list.Add(action);
            return context.Ok(list);
        }
        catch (Invalid invalid)
        {
            return context.Error(new global::app.error.@this(invalid.Message, "FormalInvalid", 400)
                { FixSuggestion = invalid.Fix });
        }
    }

    // The parse stops at the first problem; the message carries line and column.
    private sealed class Invalid(string message, string fix) : System.Exception(message)
    {
        public string Fix { get; } = fix;
    }

    // A value as written, before the row gives it its type: a JSON literal (a formal value is one), an
    // action, a list of values, or a { } of entries (a dict literal, or argument rows).
    private sealed class Literal
    {
        public string? Json;                                   // a scalar: "text", 5, true, null, "%x%"
        public global::app.goal.step.action.@this? Action;     // an action held as a value
        public List<Literal>? Items;                           // [ … ]
        public List<(string Key, string? Type, Literal Value)>? Entries;   // { … }
        public bool IsVariable;                                // written bare: %x%
        public System.Text.Json.JsonValueKind Kind;            // of a scalar
    }

    private sealed class Cursor
    {
        private readonly string _text;
        private readonly global::app.goal.step.@this _step;
        private readonly global::app.actor.context.@this _context;
        private readonly int _index;   // the step's index — a body's cursor names the step it is in
        private readonly global::app.type.item.list.@this<global::app.module.@this> _modules;
        private int _pos;

        public Cursor(string text, global::app.goal.step.@this step, global::app.actor.context.@this context, int index,
            global::app.type.item.list.@this<global::app.module.@this> modules)
        {
            _text = text;
            _step = step;
            _context = context;
            _index = index;
            _modules = modules;
        }

        public bool AtEnd { get { Space(); return _pos >= _text.Length; } }

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public void Fail(string reason, int? at = null)
        {
            if (at != null) _pos = at.Value;
            var line = 1 + _text.Take(_pos).Count(c => c == '\n');
            var column = _pos - (_text.LastIndexOf('\n', System.Math.Max(0, _pos - 1)) + 1) + 1;
            throw new Invalid($"line {line}, column {column}: {reason}", reason);
        }

        // A %variable% the answer doesn't close, quoted beside the step's own: the step's variable it starts as (the
        // longest start they share), else every variable the step writes — so the retry copies the step's text.
        private string Unclosed()
        {
            var end = _text.IndexOfAny([',', ')', ' ', '\n'], _pos + 1);
            var text = end < 0 ? _text[_pos..] : _text[_pos..end];
            var step = new global::app.type.item.variable.parser.@this(_step.Text, _context.App.type.list);
            var said = step.Variable.Select(v => v.Text).Distinct().ToList();
            var meant = step.Nearest(text);
            return meant != null
                ? $"a %variable% is not closed: the step writes `{meant}`; your answer has `{text}` — copy it exactly"
                : said.Count > 0
                    ? $"a %variable% is not closed: your answer has `{text}`; the step writes {string.Join(", ", said.Select(s => $"`{s}`"))} — copy them exactly"
                    : $"a %variable% is not closed: your answer has `{text}`";
        }

        public void Space()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
        }

        private bool Peek(string s) { Space(); return string.CompareOrdinal(_text, _pos, s, 0, s.Length) == 0; }

        private void Take(string s)
        {
            if (!Peek(s)) Fail($"expected `{s}`");
            _pos += s.Length;
        }

        // The tokens a line is read by, each anchored where the cursor stands (\G), made once.
        private static readonly System.Text.RegularExpressions.Regex Name = new(@"\G[A-Za-z_][A-Za-z0-9_]*");
        private static readonly System.Text.RegularExpressions.Regex BareKey = new(@"\G[A-Za-z_][\w-]*");
        private static readonly System.Text.RegularExpressions.Regex ActionHead = new(@"\G[A-Za-z_]\w*\.[A-Za-z_]\w*\s*\(");
        private static readonly System.Text.RegularExpressions.Regex TypeWord = new(@"\G\w+(?:<\w+>)?");
        private static readonly System.Text.RegularExpressions.Regex Symbol = new(@"\G[=!<>]+");
        private static readonly System.Text.RegularExpressions.Regex Word = new(@"\G[A-Za-z_]\w*(?![\w.(])");
        private static readonly System.Text.RegularExpressions.Regex TextWord = new(@"\G[A-Za-z_]\w*(?![\w.(%])");
        private static readonly System.Text.RegularExpressions.Regex Span = new(@"\G-?(?:P[0-9A-Za-z.]+|\d+(?:\.\d+)?(?:ms|s|m|h|d))(?![\w.(%])");
        private static readonly System.Text.RegularExpressions.Regex Quoted = new(@"\G""(?:[^""\\]|\\.)*""");
        private static readonly System.Text.RegularExpressions.Regex Number = new(@"\G-?\d+(?:\.\d+)?(?![\w.])");
        private static readonly System.Text.RegularExpressions.Regex Bare = new(@"\G[A-Za-z_/][\w./-]*");
        private static readonly System.Text.RegularExpressions.Regex Constant = new(@"\G(?:true|false|null)\b");
        private static readonly System.Text.RegularExpressions.Regex EntryType = new(@"\G(\w+(?:<\w+>)?)\s*(\?=|=)");

        private string? Match(System.Text.RegularExpressions.Regex token)
        {
            Space();
            var m = token.Match(_text, _pos);
            return m.Success ? m.Value : null;
        }

        private string Ident()
        {
            var name = Match(Name);
            if (name == null) Fail("expected a name");
            _pos += name!.Length;
            return name;
        }

        // A dict's key written bare: a name, which may hold a hyphen after its first letter (an http header,
        // `X-Probe: "x"`). A key holding anything else is written quoted.
        private string Key()
        {
            var key = Match(BareKey);
            if (key == null) Fail("expected a key");
            _pos += key!.Length;
            return key;
        }

        private bool AtAction() => Match(ActionHead) != null;

        // ---------------------------------------------------------------- actions

        /// <summary>action { ";" action } — up to <paramref name="closing"/> or the end. A clause is a clause of
        /// the action before it in the same list, and follows it there.</summary>
        public async System.Threading.Tasks.Task<List<global::app.goal.step.action.@this>> Actions(string? closing)
        {
            var actions = new List<global::app.goal.step.action.@this>();
            Attach(actions, await Action());
            while (!(closing != null ? Peek(closing) : AtEnd))
            {
                if (!Peek(";"))
                {
                    if (Peek("·")) Fail("separate a step's actions with `;`, not `·`");
                    if (AtAction()) Fail("separate a step's actions with `;`");
                    Fail(closing != null ? $"expected `;` or `{closing}`" : "expected `;` or the end of the step");
                }
                Take(";");
                Attach(actions, await Action());
            }
            return actions;
        }

        private void Attach(List<global::app.goal.step.action.@this> actions, (global::app.goal.step.action.@this Action, int At) read)
        {
            // each action names the step action it belongs to; one that belongs to none is a clause with no action
            var before = actions.Aggregate((global::app.goal.step.action.@this?)null, (anchor, a) => a.Anchor(anchor));
            if (before == null && read.Action.Belongs(before))
                Fail($"{read.Action.Module.Name}.{read.Action.Name} is a clause of the action before it; step {_index} has none — write the step's main action first, then this clause", read.At);
            actions.Add(read.Action);
        }

        // The module named, out of the ones this read was handed; null when none answers to it.
        private global::app.module.@this? Module(string name)
            => _modules.Items().FirstOrDefault(m => string.Equals(m.Name, name, System.StringComparison.OrdinalIgnoreCase));

        // action = module "." name "(" rows ")" [ "{" actions "}" ] — the action, and where it starts
        private async System.Threading.Tasks.Task<(global::app.goal.step.action.@this Action, int At)> Action()
        {
            Space();
            var start = _pos;
            var module = Ident(); Take("."); var name = Ident();
            var owner = Module(module);
            var catalog = owner?[name];
            if (catalog == null) Fail($"`{module}.{name}` is not an action", start);
            // made by its catalog element: a clause is born a clause
            var action = catalog!.Program(_step);

            Take("(");
            var given = new HashSet<string>();
            while (!Peek(")"))
            {
                if (AtEnd) Fail($"`{module}.{name}(` is not closed: expected `)`");
                if (Peek("…")) Fail($"a `…` is still there: fill in {module}.{name}'s properties, as the step gives them");
                if (given.Count > 0) Take(",");
                Space();
                var at = _pos;
                var prop = Ident();
                if (!given.Add(prop)) Fail($"`{prop}` is given twice", at);
                var property = catalog.Property[prop];
                if (property == null)
                {
                    var have = catalog.Property.Select(p => p.Name).ToList();
                    // another action the decider offered for this step that has the property is likely the one meant
                    var takes = _step.Pick.Listed.Select(l => l.Name).Where(other => other != $"{module}.{name}")
                        .FirstOrDefault(other => other.Split('.', 2) is [var m, var a] && Module(m)?[a]?.Property[prop] != null);
                    Fail($"`{module}.{name}` has no property `{prop}` (it has {(have.Count > 0 ? string.Join(", ", have) : "none")})"
                        + (takes != null ? $"; `{takes}` takes `{prop}`" : ""), at);
                }
                // a list of actions (on.error's Recovery) is program: each action read here, holding this step
                if (property!.Type.Name == "list" && property.Type.kind.Name == "action")
                {
                    Annotation(prop, "list<action>");
                    Take("=");
                    var literal = await Value();
                    if (literal.Items == null || literal.Items.Count == 0 || literal.Items.Any(i => i.Action == null))
                        Fail($"`{prop}` is a list of actions: [goal.call(Name=\"X\")]", at);
                    var actions = new global::app.goal.step.action.list.@this(_context.App.type.list[typeof(global::app.goal.step.action.list.@this)]);
                    foreach (var r in literal.Items!) actions.Add(r.Action!);
                    action.Property.Add(new global::app.type.property.@this { Name = prop, Type = property.Type, Value = actions });
                    continue;
                }
                var (row, frozen) = await Row(prop, property!);
                (frozen ? action.Default : action.Property).Add(row);
            }
            Take(")");

            if (Peek("{"))
            {
                Space();
                var brace = _pos;
                // the action says why it takes no body, when it takes none
                if (action.Nest() is { } refused) Fail(refused, brace);
                Take("{");
                if (Peek("}"))
                {
                    // an empty body reads: it breaks a rule, not the syntax — the chain check refuses it,
                    // with the step's other problems, saying where the body goes for that step
                    Take("}");
                    action.Child.Add(new global::app.goal.step.@this(_context.App.type.list) { Goal = _step.Goal, Line = _step.Line });
                    return (action, start);
                }
                var bodyStart = _pos;
                // a condition's body is a child step of this step's goal, its actions born holding it —
                // written on this step's line
                var child = new global::app.goal.step.@this(_context.App.type.list) { Goal = _step.Goal, Line = _step.Line };
                var body = new Cursor(_text, child, _context, _index, _modules) { _pos = _pos };
                var bodyActions = await body.Actions("}");
                _pos = body._pos;
                child.Text = _text[bodyStart.._pos].Trim();
                Take("}");
                foreach (var a in bodyActions) child.Code.Add(a);
                action.Child.Add(child);
            }
            return (action, start);
        }

        // An optional `: type` after a row's name. Against the slot's type it must be that type.
        private string? Annotation(string prop, string? slot)
        {
            if (!Peek(":")) return null;
            Take(":");
            Space();
            var at = _pos;
            var type = Match(TypeWord);
            if (type == null) Fail("expected a type after `:`");
            _pos += type!.Length;
            if (slot != null && slot != "item" && type != slot) Fail($"`{prop}` is {slot}, not {type}", at);
            return type;
        }

        private static string Face(global::app.type.@this type)
            => type.kind.IsEmpty ? type.Name : $"{type.Name}<{type.kind.Name}>";

        private async System.Threading.Tasks.Task<(global::app.type.property.@this Row, bool Frozen)> Row(string prop, global::app.type.property.@this property)
        {
            var slot = Face(property.Type);
            var annotation = Annotation(prop, slot);
            Space();
            // the starting line's slot copied as it stands — the name alone (or with its type): still to fill, not a value
            if (Peek(",") || Peek(")"))
                Fail($"`{prop}` is a property still to fill: write {prop}=value, the value the step gives");
            var frozen = Peek("?=");
            var options = property.Type.Values;
            // a choice's symbol option may stand bare right after its `=`: Operator=== is Operator="=="
            var afterEquals = Symbol.Match(_text, System.Math.Min(_pos + (frozen ? 2 : 1), _text.Length)).Value;
            if (Peek("==") && !(options is { Count: > 0 } && options.Contains(afterEquals)))
                Fail($"one `=` gives a value: {prop}=\"==\"");
            Take(frozen ? "?=" : "=");
            Space();
            var at = _pos;
            Literal value;
            if (options is { Count: > 0 } && Match(Symbol) is { } symbol)
            {
                // a symbol option, bare: a name out of the same closed set, written with no quotes
                if (!options.Contains(symbol)) Fail($"`{prop}` is one of {string.Join(", ", options)}; `{symbol}` is not", at);
                _pos += symbol.Length;
                value = Scalar(System.Text.Json.JsonSerializer.Serialize(symbol), System.Text.Json.JsonValueKind.String);
            }
            else if (options is { Count: > 0 } && Match(Word) is { } word && word is not ("true" or "false" or "null"))
            {
                // a choice's option may stand bare: it is a name out of a closed set
                if (!options.Contains(word)) Fail($"`{prop}` is one of {string.Join(", ", options)}; `{word}` is not", at);
                _pos += word.Length;
                value = Scalar(System.Text.Json.JsonSerializer.Serialize(word), System.Text.Json.JsonValueKind.String);
            }
            else if (property.Type.Name == "text" && Match(TextWord) is { } text && text is not ("true" or "false" or "null"))
            {
                // a single unquoted word in a text property is that text
                _pos += text.Length;
                value = Scalar(System.Text.Json.JsonSerializer.Serialize(text), System.Text.Json.JsonValueKind.String);
            }
            else if (property.Type.Name == "duration" && Match(Span) is { } span
                     && global::app.type.item.duration.@this.Resolve(span, _context) != null)
            {
                // a duration reads its own literal bare, as a choice reads its option: After=5s is After="5s",
                // After=PT5S is After="PT5S" — each in its own standard
                _pos += span.Length;
                value = Scalar(System.Text.Json.JsonSerializer.Serialize(span), System.Text.Json.JsonValueKind.String);
            }
            else value = await Value();

            var raw = value.Json != null && value.Kind == System.Text.Json.JsonValueKind.String
                ? System.Text.Json.JsonSerializer.Deserialize<string>(value.Json) : null;
            // a variable is the run's to check: what it holds is known only then, as for every typed slot
            if (options is { Count: > 0 } && !value.IsVariable && (raw == null || !options.Contains(raw)))
                Fail($"`{prop}` is one of {string.Join(", ", options)}; `{raw ?? value.Json}` is not", at);
            if (property.Type.Name == "variable")
            {
                // a variable slot given the bare name ("path") names the same variable as %path%
                if (raw != null && !raw.StartsWith('%')
                    && new global::app.type.item.variable.parser.@this(raw, _context.App.type.list).Whole is not null)
                    value = Scalar(System.Text.Json.JsonSerializer.Serialize($"%{raw}%"), System.Text.Json.JsonValueKind.String, variable: true);
                else if (!value.IsVariable)
                    Fail($"`{prop}` names a variable: write it with its % signs", at);
            }

            // an action slot holds an action as written (or a variable holding one) — a name alone is not one
            if (property.Type.Name == "action" && value.Action == null && !value.IsVariable)
                Fail($"`{prop}` takes an action: write the action itself, {prop}=goal.call(Name=\"…\")", at);

            // the type: the declared one; in an open slot the written one, else the literal's own
            var face = slot != "item" ? slot : annotation ?? LiteralType(value);
            var type = _context.App.type.list[new global::app.type.@this(TypeName(face), TypeKind(face)), _context];
            global::app.type.item.@this born;
            // a slot whose elements are records ([Records]: goal.call's Parameter) reads each a record, its name, its type
            // and its value (a %x% value born a template on its own): never rendered as one list, which would drop
            // their names. Any other slot is what the step wrote: a list of objects stays a list of objects
            var holdsRecords = property.Member?.IsDefined(typeof(global::app.Attributes.RecordsAttribute), inherit: false) == true;
            if (value.Action != null) born = value.Action;
            else if (holdsRecords && value.Items is { } records && records.All(r => r.Entries is [_]))
                born = Born(type, await Arguments(records), marks: false);
            else if (holdsRecords && !value.IsVariable)
            { Fail($"`{prop}` takes a list of arguments, each a record: {prop}=[{{name: type = value}}, {{other: type = %x%}}]", at); return default; }
            else born = Born(type, Json(value));
            // a value born a template (it holds %variables%) is declared one in the .pr too, so it renders
            // again when the .pr is read
            return (new global::app.type.property.@this
                { Name = prop, Type = born.Type is { Template: not null } templated ? templated : type, Value = born }, frozen);
        }

        // Each value born through the type's own door — the one a .pr row's value reads through.
        // The marker is born HERE, at build, and nowhere else: the programmer's own literal holding
        // %variables% is typed a plang template; its row carries the mark from then on (unless `marks` is off).
        private global::app.type.item.@this Born(global::app.type.@this type, string json, bool marks = true)
        {
            var variables = Variables(json);
            var marked = marks && type.Template == null && variables.Count > 0
                ? _context.App.type.list[new global::app.type.@this(type.Name, type.kind.Name, type.Strict,
                    new global::app.type.item.template.kind.plang.@this()), _context]
                : type;
            var bytes = Encoding.UTF8.GetBytes(json);
            var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
            utf8.Read();
            var reader = new global::app.type.item.kind.json.Reader(utf8, bytes);
            // the build reading its own answer: its templates hold every variable they list
            return marked.Read(ref reader, new global::app.type.reader.ReadContext(_context, marked.Template,
                Variable: marked.Template != null ? variables : null, IsBuilt: true));
        }

        // The variables a value written as json holds: those in each of its texts, as written.
        private List<global::app.type.item.variable.@this> Variables(string json)
        {
            var variables = new List<global::app.type.item.variable.@this>();
            if (!json.Contains('%')) return variables;
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var open = new Stack<System.Text.Json.JsonElement>([doc.RootElement]);
            while (open.TryPop(out var element))
            {
                switch (element.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.String:
                        variables.AddRange(new global::app.type.item.variable.parser.@this(element.GetString()!, _context.App.type.list).Variable);
                        break;
                    case System.Text.Json.JsonValueKind.Object:
                        foreach (var p in element.EnumerateObject().Reverse()) open.Push(p.Value);
                        break;
                    case System.Text.Json.JsonValueKind.Array:
                        foreach (var e in element.EnumerateArray().Reverse()) open.Push(e);
                        break;
                }
            }
            return variables;
        }

        private static string LiteralType(Literal v)
        {
            if (v.Action != null) return "action";
            if (v.Items != null) return "list";
            if (v.Entries != null) return "dict";
            if (v.IsVariable) return "item";
            return v.Kind switch
            {
                System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => "bool",
                System.Text.Json.JsonValueKind.Number => "number",
                System.Text.Json.JsonValueKind.Null => "item",
                _ => "text",
            };
        }

        // A list of records handed to a list property is its arguments, each typed like a property: the written
        // type, else the literal's own.
        // Each argument is its own row; one the programmer wrote with a %variable% is marked a template
        // on its row, as a top-level row is (Born) — the mark is born here, at build.
        private async System.Threading.Tasks.Task<string> Arguments(List<Literal> records)
        {
            var rows = new List<string>();
            foreach (var e in records.Select(r => r.Entries![0]))
            {
                var json = Json(e.Value);
                var variables = Variables(json).DistinctBy(x => x.Text).ToList();
                rows.Add("{\"name\":" + System.Text.Json.JsonSerializer.Serialize(e.Key)
                    + ",\"type\":{\"name\":" + System.Text.Json.JsonSerializer.Serialize(TypeName(e.Type ?? LiteralType(e.Value)))
                    + (TypeKind(e.Type ?? LiteralType(e.Value)) is { } k ? ",\"kind\":" + System.Text.Json.JsonSerializer.Serialize(k) : "")
                    + (variables.Count > 0 ? ",\"template\":\"plang\"" : "")
                    + "},\"value\":" + json
                    + (variables.Count > 0 ? ",\"variable\":" + await Json(variables) : "")
                    + "}");
            }
            return "[" + string.Join(",", rows) + "]";
        }

        // A row's variable list as the .pr writes it.
        private async System.Threading.Tasks.Task<string> Json(IReadOnlyList<global::app.type.item.variable.@this> variables)
        {
            using var ms = new System.IO.MemoryStream();
            using (var utf8 = new System.Text.Json.Utf8JsonWriter(ms))
                await new global::app.type.item.variable.serializer.Entry().Write(
                    new global::app.type.item.kind.json.Writer(utf8, global::app.View.Store, _context), variables);
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static string TypeName(string face) => face.Contains('<') ? face[..face.IndexOf('<')] : face;
        private static string? TypeKind(string face) => face.Contains('<') ? face[(face.IndexOf('<') + 1)..^1] : null;

        private string Json(Literal v)
        {
            if (v.Json != null) return v.Json;
            if (v.Items != null) return "[" + string.Join(",", v.Items.Select(Json)) + "]";
            // an object's entries keep their type: a typed one is the row a dict writes for it, its type and its value
            if (v.Entries != null)
                return "{" + string.Join(",", v.Entries.Select(e => System.Text.Json.JsonSerializer.Serialize(e.Key) + ":"
                    + (e.Type is { } type ? Typed(type, e.Value) : Json(e.Value)))) + "}";
            Fail("an action can't be a value here");
            return "";
        }

        // a value with the type it was written with, as a dict's typed entry reads back
        private string Typed(string face, Literal value)
            => "{\"type\":{\"name\":" + System.Text.Json.JsonSerializer.Serialize(TypeName(face))
               + (TypeKind(face) is { } kind ? ",\"kind\":" + System.Text.Json.JsonSerializer.Serialize(kind) : "")
               + "},\"value\":" + Json(value) + "}";

        private static Literal Scalar(string json, System.Text.Json.JsonValueKind kind, bool variable = false)
            => new() { Json = json, Kind = kind, IsVariable = variable };

        // ---------------------------------------------------------------- values

        private async System.Threading.Tasks.Task<Literal> Value()
        {
            Space();
            if (_pos >= _text.Length) Fail("expected a value");
            var c = _text[_pos];
            if (c == '"')
            {
                var s = Match(Quoted);
                if (s == null) Fail("a text is not closed");
                _pos += s!.Length;
                return Scalar(s, System.Text.Json.JsonValueKind.String);
            }
            if (c == '%')
            {
                var v = new global::app.type.item.variable.parser.@this(_text, _context.App.type.list).Read(_pos);
                if (v == null) Fail(_text.AsSpan(_pos).StartsWith("%?") ? "a `?` is still there: fill it with the value the step gives" : Unclosed());
                _pos += v!.Text.Length;
                return Scalar(System.Text.Json.JsonSerializer.Serialize(v.Text), System.Text.Json.JsonValueKind.String, variable: true);
            }
            if (c == '[')
            {
                _pos++;
                var items = new List<Literal>();
                while (!Peek("]"))
                {
                    if (AtEnd) Fail("a list is not closed: expected `]`");
                    if (items.Count > 0) Take(",");
                    items.Add(await Value());
                }
                Take("]");
                return new Literal { Items = items };
            }
            if (c == '{')
            {
                _pos++;
                var entries = new List<(string, string?, Literal)>();
                while (!Peek("}"))
                {
                    if (AtEnd) Fail("a dict is not closed: expected `}`");
                    if (entries.Count > 0) Take(",");
                    Space();
                    string key;
                    if (_pos < _text.Length && _text[_pos] == '"')
                    {
                        var k = await Value();
                        key = System.Text.Json.JsonSerializer.Deserialize<string>(k.Json!)!;
                    }
                    else key = Key();
                    Take(":");
                    Space();
                    // `key: type = value` is a typed argument row
                    string? type = null;
                    var typed = EntryType.Match(_text, _pos);
                    if (typed.Success && typed.Groups[1].Value is not ("true" or "false" or "null"))
                    {
                        type = typed.Groups[1].Value;
                        _pos += typed.Length;
                    }
                    entries.Add((key, type, await Value()));
                }
                Take("}");
                return new Literal { Entries = entries };
            }
            if (Match(Number) is { } number)
            {
                _pos += number.Length;
                return Scalar(number, System.Text.Json.JsonValueKind.Number);
            }
            if (Match(Constant) is { } word)
            {
                _pos += word.Length;
                return Scalar(word, word switch
                {
                    "true" => System.Text.Json.JsonValueKind.True,
                    "false" => System.Text.Json.JsonValueKind.False,
                    _ => System.Text.Json.JsonValueKind.Null,
                });
            }
            if (AtAction())
            {
                // an action held as a value stands alone: a modifier there has no action before it
                var actions = new List<global::app.goal.step.action.@this>();
                Attach(actions, await Action());
                return new Literal { Action = actions[0] };
            }
            if (Match(Bare) is { } bare) Fail($"a text must be in quotes: write \"{bare}\", not {bare}");
            if (_text[_pos] == '?') Fail("a `?` is still there: fill it with the value the step gives");
            Fail("expected a value: \"text\", a number, true, false, null, %variable%, [list], {dict} or an action");
            return null!;
        }
    }
}
