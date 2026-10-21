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
    private readonly global::app.type.list.@this _types;
    private readonly List<global::app.error.@this> _error = [];
    private List<variable.@this>? _variable;
    private readonly System.Lazy<variable.@this?> _whole;

    // The cursor over the reference being read, and where its body ends (its closing %).
    private int _pos, _end;

    /// <summary>A parser over <paramref name="text"/>; what it reads is born holding <paramref name="types"/>' own
    /// types — the variable, each hop, the code they make.</summary>
    public @this(string text, global::app.type.list.@this types)
    {
        _text = text ?? "";
        _types = types;
        _whole = new(One);
    }

    /// <summary>The references the text holds, in order. One that doesn't parse is left out; the
    /// reason is in <see cref="Error"/>.</summary>
    public IReadOnlyList<variable.@this> Variable => _variable ??= Scan();

    /// <summary>The one of the text's variables <paramref name="written"/> was meant to be: the one it starts most like
    /// (case aside, as variables compare), sharing more than its marker (<c>%</c>, <c>%!</c>); null when none does —
    /// what a build refusal quotes beside the answer's text, so the retry copies the step's.</summary>
    internal string? Nearest(string written)
    {
        var marker = written.StartsWith("%!") ? 2 : 1;
        int Shared(string text)
        {
            var n = 0;
            while (n < text.Length && n < written.Length && char.ToLowerInvariant(text[n]) == char.ToLowerInvariant(written[n])) n++;
            return n;
        }
        return Variable.Select(v => v.Text).Distinct().OrderByDescending(Shared).FirstOrDefault(text => Shared(text) > marker);
    }

    /// <summary>Why a reference in the text doesn't parse — empty when every one does.</summary>
    public IReadOnlyList<global::app.error.@this> Error
    {
        get { _ = Variable; return _error; }
    }

    /// <summary>The text read as a path from a value — its hops with no root: <c>choices[0].message</c>,
    /// <c>[0]</c>, <c>!type</c>. Null when it doesn't parse (the reason lands in <see cref="Error"/>).</summary>
    public code.@this? Path()
    {
        _pos = 0;
        _end = _text.Length;
        // A path that starts bare starts with a member, as if written after a dot.
        System.Func<code.Hop?, code.Hop>? bare = null;
        if (_end > 0 && _text[0] is not ('.' or '!' or '['))
        {
            bare = Named(0);
            if (bare is null) { Fail(_text, "it starts with a name"); return null; }
        }
        if (!Steps(out var next, out var start))
        {
            Fail(_text, $"'{_text[_pos]}' can't follow '{_text[..start]}'");
            return null;
        }
        var first = bare is null ? next : bare(next);
        return first is null ? null : new code.@this(first, Code);
    }

    // what the parser's reads are born holding: the app's own types
    private global::app.type.@this Code => _types[typeof(code.@this)];
    private global::app.type.@this List => _types[typeof(global::app.type.item.list.@this)];
    private global::app.type.@this Hop<THop>() where THop : code.Hop => _types[typeof(THop)];

    /// <summary>The one variable the whole text is — written with its % signs (<c>%user.name%</c>), or a bare
    /// name (<c>user</c>, the form a name slot may carry) read as <c>%user%</c>. The one way a variable is made
    /// from text. Null when the text is not exactly one variable; the reason is in <see cref="Error"/>.</summary>
    public variable.@this? Whole => _whole.Value;

    // The whole text as one variable, read once — a second read of Whole adds no second error.
    private variable.@this? One()
    {
        if (!_text.StartsWith('%'))
        {
            var named = new @this($"%{_text}%", _types);
            var whole = named.Whole;
            _error.AddRange(named.Error);
            return whole;
        }
        if (Read(0) is { } one && one.Text.Length == _text.Length) return one;
        if (_error.Count == 0) _error.Add(new global::app.error.@this($"'{_text}' is not one variable.", "InvalidVariable", 400));
        return null;
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

        var bang = _pos < _end && _text[_pos] == '!';
        if (bang) _pos++;
        var root = Name();
        if (root.Length == 0) return Fail(text, "it starts with a name");
        if (!Steps(out var next, out var start))
            return Fail(text, _pos < _end ? $"'{_text[_pos]}' can't follow '{_text[(at + 1)..start]}'" : "it ends early");
        return new variable.@this(text, new code.@this(new code.Variable(bang ? "!" + root : root, next, Hop<code.Variable>()), Code),
            _types[typeof(variable.@this)]);
    }

    private variable.@this? Fail(string text, string why)
    {
        _error.Add(new global::app.error.@this($"{text} is not a variable: {why}.", "InvalidVariable", 400));
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

    // The steps from the cursor to the end, each read before the ones after it and made holding the next: false when
    // one doesn't read (`start` where it begins, the cursor where its read stopped), true with null when none is left.
    private bool Steps(out code.Hop? first, out int start)
    {
        first = null;
        start = _pos;
        if (_pos >= _end) return true;
        var step = _text[_pos] switch
        {
            '.' => Member(start),
            '!' => Binding(start),
            '[' => Index(start),
            _ => null,
        };
        if (step == null) return false;
        if (!Steps(out var next, out start)) return false;
        first = step(next);
        return true;
    }

    // .member, ."quoted key", or .method(values)
    private System.Func<code.Hop?, code.Hop>? Member(int start)
    {
        _pos++;
        return Named(start);
    }

    // What follows a member's dot: a name, a quoted key, or a method with its values — the step made once its next is.
    private System.Func<code.Hop?, code.Hop>? Named(int start)
    {
        if (_pos < _end && _text[_pos] is '"' or '\'')
        {
            if (Quoted() is not { } key) return null;
            var text = _text[start.._pos];
            return next => new code.Property(text, key, next, Hop<code.Property>());
        }
        var name = Name();
        if (name.Length == 0) return null;
        if (_pos < _end && _text[_pos] == '(')
        {
            if (Values() is not { } values) return null;
            var text = _text[start.._pos];
            return next => new code.Method(text, name, values, next, Hop<code.Method>());
        }
        {
            var text = _text[start.._pos];
            return next => new code.Property(text, name, next, Hop<code.Property>());
        }
    }

    // !name — the binding's own plane
    private System.Func<code.Hop?, code.Hop>? Binding(int start)
    {
        _pos++;
        var name = Name();
        if (name.Length == 0) return null;
        var text = _text[start.._pos];
        return next => new code.Property(text, "!" + name, next, Hop<code.Property>());
    }

    // [0], ["k"], [idx], [%i%]
    private System.Func<code.Hop?, code.Hop>? Index(int start)
    {
        _pos++;
        var key = Value(bare: true);
        if (key == null || _pos >= _end || _text[_pos] != ']') return null;
        _pos++;
        var text = _text[start.._pos];
        return next => new code.Index(text, key, next, Hop<code.Index>());
    }

    // (value, value, …) — each a literal or a variable
    private global::app.type.item.list.@this? Values()
    {
        _pos++;
        var values = new List<global::app.type.item.@this>();
        Space();
        if (_pos < _end && _text[_pos] == ')') { _pos++; return new(values, List); }
        while (_pos < _end)
        {
            if (Value(bare: false) is not { } value) return null;
            values.Add(value);
            Space();
            if (_pos < _end && _text[_pos] == ',') { _pos++; Space(); continue; }
            if (_pos < _end && _text[_pos] == ')') { _pos++; return new(values, List); }
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
        if (c is '"' or '\'') return Quoted() is { } s ? (global::app.type.item.text.@this)s : null;
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
        var inner = new @this(_text, _types);
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
        var inner = new @this("%" + _text[start.._pos] + "%", _types);
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
