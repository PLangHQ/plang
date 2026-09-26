using System.Text;

namespace app.goal.step.action.serializer;

/// <summary>
/// Reads a step's actions from formal — <c>file.read(Path="notes.txt"); variable.set(Name=%content%, Value=%!data%)</c>
/// — the read-side twin of the formal writer (<see cref="app.channel.serializer.formal.Writer"/>). Born with
/// the step whose actions it reads, as <see cref="Reader"/> is, so every action it makes holds that step.
///
/// <para>Grammar (whitespace, new lines included, between any two tokens):</para>
/// <code>
///   actions = action { ";" action }
///   action  = module "." name "(" [ row { "," row } ] ")" [ "{" actions "}" ]
///   row     = Name [ ":" type ] ( "=" | "?=" ) value          ?= : a frozen default (the action's Default)
///   value   = "text" | number | true | false | null | %variable% | [ … ] | { … } | action
/// </code>
/// <para><c>{ }</c> is only a condition's body (its child step). A modifier follows the action it modifies,
/// in the same list — <c>file.read(…); on.error(…); cache.wrap(…)</c>, in any order: the action's
/// modifier list places each by its declared layer. A modifier's recovery is its
/// <c>Recovery=[…]</c>.</para>
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
public sealed class Formal
{
    private readonly global::app.goal.step.@this _step;

    public Formal(global::app.goal.step.@this step) => _step = step;

    /// <summary>The step's actions read from <paramref name="text"/> — Ok(action.list), or the error.</summary>
    public global::app.data.@this Read(string text, global::app.actor.context.@this context)
    {
        try
        {
            var cursor = new Cursor(text, _step, context, _step.Index);
            cursor.Space();
            if (cursor.AtEnd) cursor.Fail("a step holds at least one action");
            var list = new global::app.goal.step.action.list.@this();
            foreach (var action in cursor.Actions(null)) list.Add(action);
            return context.Ok(list);
        }
        catch (Invalid invalid)
        {
            return context.Error(new global::app.error.Error(invalid.Message, "FormalInvalid", 400)
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
        private int _pos;

        public Cursor(string text, global::app.goal.step.@this step, global::app.actor.context.@this context, int index)
        {
            _text = text;
            _step = step;
            _context = context;
            _index = index;
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

        private string? Match(string pattern)
        {
            Space();
            var m = new System.Text.RegularExpressions.Regex(@"\G(?:" + pattern + ")").Match(_text, _pos);
            return m.Success ? m.Value : null;
        }

        private string Ident()
        {
            var name = Match(@"[A-Za-z_][A-Za-z0-9_]*");
            if (name == null) Fail("expected a name");
            _pos += name!.Length;
            return name;
        }

        private bool AtAction() => Match(@"[A-Za-z_]\w*\.[A-Za-z_]\w*\s*\(") != null;

        // ---------------------------------------------------------------- actions

        /// <summary>action { ";" action } — up to <paramref name="closing"/> or the end. A modifier modifies
        /// the action before it in the same list; its modifier list places it by its layer.</summary>
        public List<global::app.goal.step.action.@this> Actions(string? closing)
        {
            var actions = new List<global::app.goal.step.action.@this>();
            Attach(actions, Action());
            while (!(closing != null ? Peek(closing) : AtEnd))
            {
                if (!Peek(";"))
                {
                    if (Peek("·")) Fail("separate a step's actions with `;`, not `·`");
                    if (AtAction()) Fail("separate a step's actions with `;`");
                    Fail(closing != null ? $"expected `;` or `{closing}`" : "expected `;` or the end of the step");
                }
                Take(";");
                Attach(actions, Action());
            }
            return actions;
        }

        private void Attach(List<global::app.goal.step.action.@this> actions, (global::app.goal.step.action.@this Action, int At) read)
        {
            if (read.Action is not global::app.goal.step.action.modifier.@this modifier) { actions.Add(read.Action); return; }
            if (actions.Count == 0)
                Fail($"{modifier.Module.Name}.{modifier.Name} modifies the action before it; step {_index} has none", read.At);
            actions[^1].Modifier.Add(modifier);
        }

        private global::app.goal.step.action.@this? Catalog(string module, string name)
            => _context.App.Module.Contains(module) ? _context.App.Module[module][name] : null;

        // action = module "." name "(" rows ")" [ "{" actions "}" ] — the action, and where it starts
        private (global::app.goal.step.action.@this Action, int At) Action()
        {
            Space();
            var start = _pos;
            var module = Ident(); Take("."); var name = Ident();
            var catalog = Catalog(module, name);
            if (catalog == null) Fail($"`{module}.{name}` is not an action", start);
            var isModifier = catalog is global::app.goal.step.action.modifier.@this;
            var isCondition = module == "condition" && name is "if" or "elseif" or "else";

            global::app.goal.step.action.@this action = catalog is global::app.goal.step.action.modifier.@this
                ? new global::app.goal.step.action.modifier.@this { Step = _step, Synthetic = false }
                : new global::app.goal.step.action.@this { Step = _step, Synthetic = false };
            action.Module = _context.App.Module[module];
            action.Name = name;

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
                if (prop == "Recovery" && isModifier && module == "on" && name == "error")
                {
                    WrittenType(prop, "list<action>");
                    Take("=");
                    var recovery = Value(null);
                    if (recovery.Items == null || recovery.Items.Count == 0 || recovery.Items.Any(i => i.Action == null))
                        Fail("`Recovery` is a list of actions: [goal.call(Name=\"X\")]", at);
                    foreach (var r in recovery.Items!) action.Recovery.Add(r.Action!);
                    continue;
                }
                var declared = catalog!.Property[prop];
                if (declared == null)
                {
                    var have = catalog.Property.Select(p => p.Name).ToList();
                    if (isModifier && module == "on" && name == "error") have.Add("Recovery");
                    Fail($"`{module}.{name}` has no property `{prop}` (it has {(have.Count > 0 ? string.Join(", ", have) : "none")})", at);
                }
                var (row, frozen) = Row(prop, declared!);
                (frozen ? action.Default : action.Property).Add(row);
            }
            Take(")");

            if (Peek("{"))
            {
                Space();
                var brace = _pos;
                if (isModifier)
                    Fail($"`{module}.{name}` takes no {{ }}: write the action first, then {module}.{name} after it — " +
                         $"next.action(…); {module}.{name}(…)", brace);
                if (!isCondition)
                    Fail($"`{module}.{name}` contains no actions: only a condition takes {{ }} (its body); " +
                         $"write the actions one after the other: {module}.{name}(…); next.action(…)");
                Take("{");
                if (Peek("}"))
                {
                    // an empty body reads: it breaks a rule, not the syntax — the chain check refuses it,
                    // with the step's other problems, saying where the body goes for that step
                    Take("}");
                    action.Child.Add(new global::app.goal.step.@this { Goal = _step.Goal, Line = _step.Line });
                    return (action, start);
                }
                var bodyStart = _pos;
                // a condition's body is a child step of this step's goal, its actions born holding it —
                // written on this step's line
                var child = new global::app.goal.step.@this { Goal = _step.Goal, Line = _step.Line };
                var body = new Cursor(_text, child, _context, _index) { _pos = _pos };
                var bodyActions = body.Actions("}");
                _pos = body._pos;
                child.Text = _text[bodyStart.._pos].Trim();
                Take("}");
                foreach (var a in bodyActions) child.Code.Add(a);
                action.Child.Add(child);
            }
            return (action, start);
        }

        // An optional `: type` after a row's name. Against a declared type it must be that type.
        private string? WrittenType(string prop, string? declared)
        {
            if (!Peek(":")) return null;
            Take(":");
            Space();
            var at = _pos;
            var written = Match(@"\w+(?:<\w+>)?");
            if (written == null) Fail("expected a type after `:`");
            _pos += written!.Length;
            if (declared != null && declared != "item" && written != declared) Fail($"`{prop}` is {declared}, not {written}", at);
            return written;
        }

        private static string Face(global::app.type.@this type)
            => type.Kind is { } kind ? $"{type.Name}<{kind.Name}>" : type.Name;

        private (global::app.type.property.@this Row, bool Frozen) Row(string prop, global::app.type.property.@this declared)
        {
            var declaredFace = Face(declared.Type);
            var written = WrittenType(prop, declaredFace);
            Space();
            var frozen = Peek("?=");
            var options = declared.Type.Values;
            // a choice's symbol option may stand bare right after its `=`: Operator=== is Operator="=="
            var afterEquals = new System.Text.RegularExpressions.Regex(@"\G[=!<>]+").Match(_text, System.Math.Min(_pos + (frozen ? 2 : 1), _text.Length)).Value;
            if (Peek("==") && !(options is { Count: > 0 } && options.Contains(afterEquals)))
                Fail($"one `=` gives a value: {prop}=\"==\"");
            Take(frozen ? "?=" : "=");
            Space();
            var at = _pos;
            Literal value;
            if (options is { Count: > 0 } && Match(@"[=!<>]+") is { } symbol)
            {
                // a symbol option, bare: a name out of the same closed set, written with no quotes
                if (!options.Contains(symbol)) Fail($"`{prop}` is one of {string.Join(", ", options)}; `{symbol}` is not", at);
                _pos += symbol.Length;
                value = Scalar(System.Text.Json.JsonSerializer.Serialize(symbol), System.Text.Json.JsonValueKind.String);
            }
            else if (options is { Count: > 0 } && Match(@"[A-Za-z_]\w*(?![\w.(])") is { } word && word is not ("true" or "false" or "null"))
            {
                // a choice's option may stand bare: it is a name out of a closed set
                if (!options.Contains(word)) Fail($"`{prop}` is one of {string.Join(", ", options)}; `{word}` is not", at);
                _pos += word.Length;
                value = Scalar(System.Text.Json.JsonSerializer.Serialize(word), System.Text.Json.JsonValueKind.String);
            }
            else if (declared.Type.Name == "text" && Match(@"[A-Za-z_]\w*(?![\w.(%])") is { } text && text is not ("true" or "false" or "null"))
            {
                // a single unquoted word in a text property is that text
                _pos += text.Length;
                value = Scalar(System.Text.Json.JsonSerializer.Serialize(text), System.Text.Json.JsonValueKind.String);
            }
            else value = Value(declared.Type.Name);

            var raw = value.Json != null && value.Kind == System.Text.Json.JsonValueKind.String
                ? System.Text.Json.JsonSerializer.Deserialize<string>(value.Json) : null;
            if (options is { Count: > 0 } && (raw == null || !options.Contains(raw)))
                Fail($"`{prop}` is one of {string.Join(", ", options)}; `{raw ?? value.Json}` is not", at);
            if (declared.Type.Name == "variable")
            {
                // a variable slot given the bare name ("path") names the same variable as %path%
                if (raw != null && !raw.StartsWith('%') && System.Text.RegularExpressions.Regex.IsMatch(raw, @"^!?[A-Za-z_][\w.\[\]]*$"))
                    value = Scalar(System.Text.Json.JsonSerializer.Serialize($"%{raw}%"), System.Text.Json.JsonValueKind.String, variable: true);
                else if (!value.IsVariable)
                    Fail($"`{prop}` names a variable: write it with its % signs", at);
            }

            // the type: the declared one; in an open slot the written one, else the literal's own
            var typeName = declaredFace != "item" ? declaredFace : written ?? LiteralType(value);
            var type = _context.App.Type[typeName];
            global::app.type.item.@this born;
            if (value.Action != null) born = value.Action;
            else if (declared.Type.Name == "list" && value.Entries != null) born = Born(type, Arguments(value));
            else if (value.Entries != null && value.Entries.Any(e => e.Type != null))
            { Fail($"`{prop}` takes a value, not argument rows: a typed entry (`name: type = value`) belongs to a list of arguments", at); return default; }
            else born = Born(type, Json(value));
            return (new global::app.type.property.@this { Name = prop, Type = type, Value = born }, frozen);
        }

        // Each value born through the type's own door — the one a .pr row's value reads through.
        private global::app.type.item.@this Born(global::app.type.@this type, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
            utf8.Read();
            var reader = new global::app.channel.serializer.json.Reader(utf8, bytes);
            return type.Read(ref reader, new global::app.type.reader.ReadContext(_context, "plang"));
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

        // A dict handed to a list property is its argument rows, each typed like a property: the written
        // type, else the literal's own.
        private string Arguments(Literal v)
        {
            var rows = v.Entries!.Select(e =>
                "{\"name\":" + System.Text.Json.JsonSerializer.Serialize(e.Key)
                + ",\"type\":{\"name\":" + System.Text.Json.JsonSerializer.Serialize(TypeName(e.Type ?? LiteralType(e.Value)))
                + (TypeKind(e.Type ?? LiteralType(e.Value)) is { } k ? ",\"kind\":" + System.Text.Json.JsonSerializer.Serialize(k) : "")
                + "},\"value\":" + Json(e.Value) + "}");
            return "[" + string.Join(",", rows) + "]";
        }

        private static string TypeName(string face) => face.Contains('<') ? face[..face.IndexOf('<')] : face;
        private static string? TypeKind(string face) => face.Contains('<') ? face[(face.IndexOf('<') + 1)..^1] : null;

        private string Json(Literal v)
        {
            if (v.Json != null) return v.Json;
            if (v.Items != null) return "[" + string.Join(",", v.Items.Select(Json)) + "]";
            if (v.Entries != null)
                return "{" + string.Join(",", v.Entries.Select(e => System.Text.Json.JsonSerializer.Serialize(e.Key) + ":" + Json(e.Value))) + "}";
            Fail("an action can't be a value here");
            return "";
        }

        private static Literal Scalar(string json, System.Text.Json.JsonValueKind kind, bool variable = false)
            => new() { Json = json, Kind = kind, IsVariable = variable };

        // ---------------------------------------------------------------- values

        private Literal Value(string? declared)
        {
            Space();
            if (_pos >= _text.Length) Fail("expected a value");
            var c = _text[_pos];
            if (c == '"')
            {
                var s = Match(@"""(?:[^""\\]|\\.)*""");
                if (s == null) Fail("a text is not closed");
                _pos += s!.Length;
                return Scalar(s, System.Text.Json.JsonValueKind.String);
            }
            if (c == '%')
            {
                var v = Match(@"%[^%\s]+%");
                if (v == null) Fail(_text.AsSpan(_pos).StartsWith("%?") ? "a `?` is still there: fill it with the value the step gives" : "a %variable% is not closed");
                _pos += v!.Length;
                return Scalar(System.Text.Json.JsonSerializer.Serialize(v), System.Text.Json.JsonValueKind.String, variable: true);
            }
            if (c == '[')
            {
                // argument rows written as a list — only where the slot takes rows (a list slot); in an
                // open slot `[{Role: "user", …}]` is a list of dicts
                if (declared == "list" && Match(@"\[\s*(\{\s*)?[A-Za-z_]\w*\s*[=:]") != null)
                    Fail("arguments are written as one dict: Parameter={name: \"value\", other: %x%}");
                _pos++;
                var items = new List<Literal>();
                while (!Peek("]"))
                {
                    if (AtEnd) Fail("a list is not closed: expected `]`");
                    if (items.Count > 0) Take(",");
                    items.Add(Value(null));
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
                        var k = Value(null);
                        key = System.Text.Json.JsonSerializer.Deserialize<string>(k.Json!)!;
                    }
                    else key = Ident();
                    Take(":");
                    Space();
                    // `key: type = value` is a typed argument row
                    string? type = null;
                    var typed = new System.Text.RegularExpressions.Regex(@"\G(\w+(?:<\w+>)?)\s*(\?=|=)").Match(_text, _pos);
                    if (typed.Success && typed.Groups[1].Value is not ("true" or "false" or "null"))
                    {
                        type = typed.Groups[1].Value;
                        _pos += typed.Length;
                    }
                    entries.Add((key, type, Value(null)));
                }
                Take("}");
                return new Literal { Entries = entries };
            }
            if (Match(@"-?\d+(?:\.\d+)?(?![\w.])") is { } number)
            {
                _pos += number.Length;
                return Scalar(number, System.Text.Json.JsonValueKind.Number);
            }
            foreach (var (word, kind) in new[] { ("true", System.Text.Json.JsonValueKind.True), ("false", System.Text.Json.JsonValueKind.False), ("null", System.Text.Json.JsonValueKind.Null) })
                if (Match(word + @"\b") != null) { _pos += word.Length; return Scalar(word, kind); }
            if (AtAction())
            {
                // an action held as a value stands alone: a modifier there has no action before it
                var held = new List<global::app.goal.step.action.@this>();
                Attach(held, Action());
                return new Literal { Action = held[0] };
            }
            if (Match(@"[A-Za-z_][\w./-]*") is { } bare) Fail($"a text is quoted: \"{bare}\"");
            if (_text[_pos] == '?') Fail("a `?` is still there: fill it with the value the step gives");
            Fail("expected a value: \"text\", a number, true, false, null, %variable%, [list], {dict} or an action");
            return null!;
        }
    }
}
