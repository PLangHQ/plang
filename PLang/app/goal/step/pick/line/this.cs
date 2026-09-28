namespace app.goal.step.pick.line;

/// <summary>
/// A step's pre-filled formal line, as pick hands it to the LLM: each certain action takes its own place in it
/// (<c>action.Prefill</c>), with <c>?</c> where a value is still to fill. On a lone if — no elseif or else on the
/// step, nothing indented below it — the if opens a body, and the step's other actions go inside its
/// <c>{ }</c>, as the chain check wants them; a chain stays flat, since the line can't say which branch an
/// action belongs to.
/// </summary>
internal sealed class @this(bool nests)
{
    private readonly List<string> _line = new();
    private int _head = -1;          // the if whose body is open
    private List<string>? _body;     // its body, where the step's actions go

    /// <summary>A step action: after the ones before it — in the open body when there is one — or where a
    /// clause left <c>?</c> for it. An if (<paramref name="opens"/>) on a lone-if line opens the body.</summary>
    internal void Add(string call, bool opens)
    {
        if (opens && nests && _body == null)
        {
            _line.Add(call);
            _head = _line.Count - 1;
            _body = new();
            return;
        }
        var into = _body ?? _line;
        if (into.Count > 0 && into[0] == "?") into[0] = call;
        else into.Add(call);
    }

    /// <summary>A loop: first, since what follows it is what it runs.</summary>
    internal void Lead(string call)
    {
        _line.Insert(0, call);
        if (_head >= 0) _head++;
    }

    /// <summary>A clause: right after the first action where the step's actions go — the action it is a clause
    /// of (<c>?</c> while that isn't known).</summary>
    internal void Insert(string call)
    {
        var into = _body ?? _line;
        if (into.Count == 0) into.Add("?");
        into.Insert(1, call);
    }

    /// <summary>The step's <c>write to %x%</c>: last, after the if.</summary>
    internal void Append(string call) => _line.Add(call);

    /// <summary>The line in formal; null when nothing is certain.</summary>
    public override string? ToString()
    {
        if (_line.Count == 0) return null;
        return string.Join("; ", _line.Select((call, i) =>
            i == _head && _body is { Count: > 0 } body ? $"{call} {{ {string.Join("; ", body)} }}" : call));
    }
}
