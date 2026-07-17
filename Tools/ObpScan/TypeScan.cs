using System.Text;
using Microsoft.CodeAnalysis;

namespace ObpScan;

/// <summary>A type under scan — owns rendering its own member table and the missing-type summary.
/// It builds Members and asks each for its own verdicts; it does not compute smells about them.</summary>
public sealed class TypeScan
{
    private readonly INamedTypeSymbol _type;
    private readonly Solution _solution;

    public TypeScan(INamedTypeSymbol type, Solution solution)
    {
        _type = type;
        _solution = solution;
    }

    public string FullName => $"{_type.ContainingNamespace?.ToDisplayString()}.{_type.Name}".TrimStart('.');

    public async Task<string> Render()
    {
        var declNs = _type.ContainingNamespace?.ToDisplayString() ?? "";
        var sb = new StringBuilder();
        sb.AppendLine().AppendLine($"## {FullName}").AppendLine();
        sb.AppendLine("| Member | Lines | Name flag | Callers (namespaces) | Ownership hint |");
        sb.AppendLine("|--------|-------|-----------|----------------------|----------------|");

        int members = 0, nameFlags = 0, longFlags = 0, misplaced = 0;
        var clusters = new Dictionary<string, int>();

        foreach (var symbol in _type.GetMembers().Where(Member.IsScannable))
        {
            members++;
            var member = new Member(symbol, declNs);
            var callers = await member.Callers(_solution);
            var ownership = member.Judge(callers);

            if (member.Name.IsCompound) nameFlags++;
            if (member.Lines > 15) longFlags++;
            if (ownership.ForeignConcept is { } fc)
            {
                misplaced++;
                clusters[fc] = clusters.GetValueOrDefault(fc) + 1;
            }

            var lenCell = member.Lines > 15 ? $"**{member.Lines}**" : member.Lines.ToString();
            var callerCell = callers.Count == 0 ? "(none/internal)"
                : string.Join(", ", callers.Distinct().Select(Concept.Short).Take(4));
            sb.AppendLine($"| {symbol.Name} | {lenCell} | {member.Name.Flag} | {callerCell} | {ownership.Verdict} |");
        }

        sb.AppendLine();
        sb.AppendLine($"**{members} members — flagged: {nameFlags} name, {longFlags} long, {misplaced} misplaced-hint.**");
        foreach (var (concept, count) in clusters.Where(c => c.Value >= 3).OrderByDescending(c => c.Value))
            sb.AppendLine($"> ⚠ **missing-type signal**: {count} members whose callers live in `{concept}` — "
                + $"a `{concept}` concept is likely trapped in this type.");
        return sb.ToString();
    }
}
