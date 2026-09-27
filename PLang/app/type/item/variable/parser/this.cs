namespace app.type.item.variable.parser;

/// <summary>
/// The one definition of a reference: <c>%</c>, then a path starting with a letter, <c>_</c> or
/// <c>!</c>, closing at the first <c>%</c> outside quotes, parentheses and brackets (so
/// <c>%user.address[%i%].city%</c> and <c>%name.replace("%", "")%</c> are one reference each). A
/// space outside them ends the scan: <c>50% of %total%</c> holds one reference. Born over a text;
/// <see cref="Variable"/> is every reference the text holds, <see cref="Read"/> the one at a
/// position. Each hop is read here as its own piece: the root, <c>.member</c>, <c>."quoted key"</c>,
/// <c>!name</c>, <c>[key]</c>, <c>.method(values)</c>.
/// </summary>
public sealed class @this
{
    private readonly string _text;
    private readonly List<global::app.error.Error> _error = [];
    private List<variable.@this>? _variable;

    // The cursor over the reference being read, and where its body ends (its closing %).
    private int _pos, _end;

    public @this(string text) => _text = text ?? "";

    /// <summary>The references the text holds, in order. One that doesn't parse is left out; the
    /// reason is in <see cref="Error"/>.</summary>
    public IReadOnlyList<variable.@this> Variable => _variable ??= Scan();

    /// <summary>Why a reference in the text doesn't parse — empty when every one does.</summary>
    public IReadOnlyList<global::app.error.Error> Error
    {
        get { _ = Variable; return _error; }
    }

    /// <summary>The text read as a path from a value — its hops with no root: <c>choices[0].message</c>,
    /// <c>[0]</c>, <c>!type</c>. Null when it doesn't parse (the reason lands in <see cref="Error"/>).</summary>
    public code.@this? Path()
    {
        _pos = 0;
        _end = _text.Length;
        var hops = new List<code.Hop>();
        // A path that starts bare starts with a member, as if written after a dot.
        if (_end > 0 && _text[0] is not ('.' or '!' or '['))
        {
            if (Named(0) is not { } first) { Fail(_text, "it starts with a name"); return null; }
            hops.Add(first);
        }
        while (_pos < _end)
        {
            var start = _pos;
            var hop = _text[_pos] switch
            {
                '.' => Member(start),
                '!' => Binding(start),
                '[' => Index(start),
                _ => null,
            };
            if (hop == null) { Fail(_text, $"'{_text[_pos]}' can't follow '{_text[..start]}'"); return null; }
            hops.Add(hop);
        }
        return new code.@this(hops);
    }

    /// <summary>The one variable the whole text is — written with its % signs (<c>%user.name%</c>), or a bare
    /// name (<c>user</c>, the form a name slot may carry) read as <c>%user%</c>. The one way a variable is made
    /// from text. Null when the text is not exactly one variable; the reason is in <see cref="Error"/>.</summary>
    public variable.@this? Whole
    {
        get
        {
            if (!_text.StartsWith('%'))
            {
                var named = new @this($"%{_text}%");
                var whole = named.Whole;
                _error.AddRange(named.Error);
                return whole;
            }
            if (Read(0) is { } one && one.Text.Length == _text.Length) return one;
            if (_error.Count == 0) _error.Add(new global::app.error.Error($"'{_text}' is not one variable.", "InvalidVariable", 400));
            return null;
        }
    }

    /// <summary>The reference whose opening <c>%</c> is at <paramref name="at"/>, or null: none opens
    /// there, or it doesn't parse (the reason lands in <see cref="Error"/>). Its <c>Text</c> is exactly
    /// what was written, so the reader continues after it.</summary>
    public variable.@this? Read(int at)
    {
        var close = Close(at);
        return close < 0 ? null : Body(at, close);
    }

    private List<variable.@this> Scan()
    {
        var found = new List<variable.@this>();
        for (int i = 0; i < _text.Length; i++)
        {
            if (_text[i] != '%') continue;
            var close = Close(i);
            if (close < 0) continue;
            if (Body(i, close) is { } v) found.Add(v);
            i = close;
        }
        return found;
    }

    // The closing % of a reference opening at `at`, or -1 when none opens there.
    private int Close(int at)
    {
        if (at + 1 >= _text.Length || _text[at] != '%' || !IsStart(_text[at + 1])) return -1;
        int depth = 0;
        char quote = '\0';
        for (int i = at + 1; i < _text.Length; i++)
        {
            var c = _text[i];
            if (quote != '\0')
            {
                if (c == '\\') i++;
                else if (c == quote) quote = '\0';
                continue;
            }
            if (c is '"' or '\'') quote = c;
            else if (c is '(' or '[') depth++;
            else if (c is ')' or ']') depth--;
            else if (depth == 0 && c == '%') return i;
            else if (depth == 0 && char.IsWhiteSpace(c)) return -1;
        }
        return -1;
    }

    private static bool IsStart(char c) => char.IsLetter(c) || c == '_' || c == '!';

    // The reference between `at` and `close`, read hop by hop.
    private variable.@this? Body(int at, int close)
    {
        var text = _text[at..(close + 1)];
        _pos = at + 1;
        _end = close;
        var hops = new List<code.Hop>();

        var bang = _pos < _end && _text[_pos] == '!';
        if (bang) _pos++;
        var root = Name();
        if (root.Length == 0) return Fail(text, "it starts with a name");
        hops.Add(new code.Variable(bang ? "!" + root : root));

        while (_pos < _end)
        {
            var start = _pos;
            var hop = _text[_pos] switch
            {
                '.' => Member(start),
                '!' => Binding(start),
                '[' => Index(start),
                _ => null,
            };
            if (hop == null)
                return Fail(text, _pos < _end ? $"'{_text[_pos]}' can't follow '{_text[(at + 1)..start]}'" : "it ends early");
            hops.Add(hop);
        }
        return new variable.@this(text, new code.@this(hops));
    }

    private variable.@this? Fail(string text, string why)
    {
        _error.Add(new global::app.error.Error($"{text} is not a variable: {why}.", "InvalidVariable", 400));
        return null;
    }

    // A name: everything up to the next hop, bracket, parenthesis, quote, space or the end.
    private string Name()
    {
        var start = _pos;
        while (_pos < _end && _text[_pos] is not ('.' or '!' or '[' or ']' or '(' or ')' or '"' or '\'' or '%' or ',')
               && !char.IsWhiteSpace(_text[_pos]))
            _pos++;
        return _text[start.._pos];
    }

    // .member, ."quoted key", or .method(values)
    private code.Hop? Member(int start)
    {
        _pos++;
        return Named(start);
    }

    // What follows a member's dot: a name, a quoted key, or a method with its values.
    private code.Hop? Named(int start)
    {
        if (_pos < _end && _text[_pos] is '"' or '\'')
            return Quoted() is { } key ? new code.Property(_text[start.._pos], key) : null;
        var name = Name();
        if (name.Length == 0) return null;
        if (_pos < _end && _text[_pos] == '(')
            return Values() is { } values ? new code.Method(_text[start.._pos], name, values) : null;
        return new code.Property(_text[start.._pos], name);
    }

    // !name — the binding's own plane
    private code.Hop? Binding(int start)
    {
        _pos++;
        var name = Name();
        return name.Length == 0 ? null : new code.Property(_text[start.._pos], "!" + name);
    }

    // [0], ["k"], [idx], [%i%]
    private code.Hop? Index(int start)
    {
        _pos++;
        var key = Value(bare: true);
        if (key == null || _pos >= _end || _text[_pos] != ']') return null;
        _pos++;
        return new code.Index(_text[start.._pos], key);
    }

    // (value, value, …) — each a literal or a variable
    private global::app.type.item.list.@this? Values()
    {
        _pos++;
        var values = new List<global::app.type.item.@this>();
        Space();
        if (_pos < _end && _text[_pos] == ')') { _pos++; return new(values); }
        while (_pos < _end)
        {
            if (Value(bare: false) is not { } value) return null;
            values.Add(value);
            Space();
            if (_pos < _end && _text[_pos] == ',') { _pos++; Space(); continue; }
            if (_pos < _end && _text[_pos] == ')') { _pos++; return new(values); }
            return null;
        }
        return null;
    }

    // A value: "text" or 'text', a number, true/false, a %variable%, or — inside brackets — a bare
    // path naming a variable.
    private global::app.type.item.@this? Value(bool bare)
    {
        if (_pos >= _end) return null;
        var c = _text[_pos];
        if (c is '"' or '\'') return Quoted() is { } s ? new global::app.type.item.text.@this(s) : null;
        if (char.IsDigit(c) || (c == '-' && _pos + 1 < _end && char.IsDigit(_text[_pos + 1]))) return Number();
        if (c == '%') return Nested(_pos);
        if (bare) return Bare();
        var word = Name();
        return word switch
        {
            "true" => (global::app.type.item.@bool.@this)true,
            "false" => (global::app.type.item.@bool.@this)false,
            _ => null,
        };
    }

    // A reference written inside this one; the cursor continues after it.
    private variable.@this? Nested(int at)
    {
        var inner = new @this(_text);
        var found = inner.Read(at);
        _error.AddRange(inner._error);
        if (found != null) _pos = at + found.Text.Length;
        return found;
    }

    // A bare path inside brackets: [idx], [askInfo.gui], [items[0]] — a variable.
    private variable.@this? Bare()
    {
        var start = _pos;
        int depth = 0;
        while (_pos < _end && !(depth == 0 && _text[_pos] == ']'))
        {
            if (_text[_pos] == '[') depth++;
            else if (_text[_pos] == ']') depth--;
            _pos++;
        }
        var inner = new @this("%" + _text[start.._pos] + "%");
        var found = inner.Read(0);
        _error.AddRange(inner._error);
        return found;
    }

    private string? Quoted()
    {
        var quote = _text[_pos++];
        var sb = new System.Text.StringBuilder();
        while (_pos < _end)
        {
            var c = _text[_pos++];
            if (c == quote) return sb.ToString();
            if (c == '\\' && _pos < _end) c = _text[_pos++];
            sb.Append(c);
        }
        return null;
    }

    private global::app.type.item.number.@this? Number()
    {
        var start = _pos;
        if (_text[_pos] == '-') _pos++;
        while (_pos < _end && (char.IsDigit(_text[_pos]) || _text[_pos] == '.')) _pos++;
        var written = _text[start.._pos];
        if (long.TryParse(written, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var whole))
            return whole;
        if (decimal.TryParse(written, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var part))
            return part;
        return null;
    }

    private void Space()
    {
        while (_pos < _end && char.IsWhiteSpace(_text[_pos])) _pos++;
    }
}
