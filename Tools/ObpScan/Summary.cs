using System.Text;

namespace ObpScan;

/// <summary>The scan's running tally — owns the counters and the missing-type detection, and
/// renders its own footer. Extracted from TypeScan.Render so rendering rows and accumulating
/// verdicts are two responsibilities on two objects (the OBP the tool enforces, applied to itself).
/// Each row reports its verdict via <see cref="Add"/>; the type asks for the <see cref="Footer"/>.</summary>
public sealed class Summary
{
    private int _members, _nameFlags, _longFlags, _misplaced;
    private readonly Dictionary<string, int> _clusters = new();

    public void Add(MemberName name, int lines, Ownership ownership)
    {
        _members++;
        if (name.IsFlagged) _nameFlags++;
        if (lines > 15) _longFlags++;
        if (ownership.ForeignConcept is { } concept)
        {
            _misplaced++;
            _clusters[concept] = _clusters.GetValueOrDefault(concept) + 1;
        }
    }

    public string Footer()
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"**{_members} members — flagged: {_nameFlags} name, {_longFlags} long, {_misplaced} misplaced-hint.**");
        foreach (var (concept, count) in _clusters.Where(c => c.Value >= 3).OrderByDescending(c => c.Value))
            sb.AppendLine($"> ⚠ **missing-type signal**: {count} members whose callers live in `{concept}` — "
                + $"a `{concept}` concept is likely trapped in this type.");
        return sb.ToString();
    }
}
