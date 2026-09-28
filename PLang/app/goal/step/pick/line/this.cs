namespace app.goal.step.pick.line;

/// <summary>
/// A step's pre-filled formal line, as pick hands it to the LLM: each certain action takes its own place in it
/// (<c>action.Prefill</c>), with <c>?</c> where a value is still to fill. On a lone if — no elseif or else on the
/// step, nothing indented below it — the if opens a body, and the step's other actions go inside its
/// <c>{ }</c>, as the chain check wants them; a chain stays flat, since the line can't say which branch an
/// action belongs to. A loop leads; a keep (variable.set) follows the actions that produce the value it keeps.
/// </summary>
internal sealed class @this(bool nests)
{
    private readonly List<string> _line = new();
    private int _head = -1;          // the if whose body is open
    private List<string>? _body;     // its body, where the step's actions go
    private bool _produced;          // a step action produces a value
    // a keep's place, held until the line knows whether a value is produced: its marker, and both calls
    private readonly List<(string Marker, string Stands, string Follows)> _kept = new();
    private int _appended;           // the write-to sets appended last

    /// <summary>A step action: after the ones before it — in the open body when there is one — or where a
    /// clause left <c>?</c> for it. An if (<paramref name="opens"/>) on a lone-if line opens the body; an
    /// action that <paramref name="produces"/> a value is what a keep keeps.</summary>
    internal void Add(string call, bool opens, bool produces = false)
    {
        _produced |= produces;
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

    /// <summary>A keep: where it <paramref name="stands"/> when no action produces a value; after them, as
    /// <paramref name="follows"/> (keeping <c>%!data%</c>), when one does — decided when the line is written.</summary>
    internal void Keep(string stands, string follows)
    {
        var marker = $"\u0001keep{_kept.Count}";
        _kept.Add((marker, stands, follows));
        Add(marker, opens: false);
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
    internal void Append(string call)
    {
        _line.Add(call);
        _appended++;
    }

    /// <summary>The line in formal; null when nothing is certain.</summary>
    public override string? ToString()
    {
        if (_line.Count == 0) return null;
        var line = Placed(_line);
        return string.Join("; ", line.Select((call, i) =>
            i == _head && _body is { Count: > 0 } body ? $"{call} {{ {string.Join("; ", Placed(body))} }}" : call));
    }

    // A list with its keeps in place: where they stand, or — a value produced — after the list's own actions,
    // before a write-to appended last.
    private List<string> Placed(List<string> calls)
    {
        if (!_produced)
            return calls.Select(c => _kept.FirstOrDefault(k => k.Marker == c) is { Marker: not null } k ? k.Stands : c).ToList();
        var own = calls.Where(c => _kept.All(k => k.Marker != c)).ToList();
        var kept = calls.Select(c => _kept.FirstOrDefault(k => k.Marker == c)).Where(k => k.Marker != null).Select(k => k.Follows);
        // the appended write-to stays last
        var appended = ReferenceEquals(calls, _line) ? _appended : 0;
        var tail = own.Skip(own.Count - appended).ToList();
        return own.Take(own.Count - appended).Concat(kept).Concat(tail).ToList();
    }
}
