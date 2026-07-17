using System.Text;
using Microsoft.CodeAnalysis;

namespace ObpScan;

/// <summary>A type under scan — owns rendering its own member table. It builds Members and asks each
/// for its own verdicts (it does not compute smells about them); the running tally + missing-type
/// detection belong to <see cref="Summary"/>, which each row reports to.</summary>
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
        var members = _type.GetMembers().Where(Member.IsScannable)
            .Select(s => new Member(s, declNs)).ToList();

        // resolve every member's callers concurrently — the per-member SymbolFinder search was the
        // sequential bottleneck; the searches are independent, so they run in parallel.
        var callers = await Task.WhenAll(members.Select(m => m.Callers(_solution)));

        var sb = new StringBuilder();
        sb.AppendLine().AppendLine($"## {FullName}").AppendLine();
        sb.AppendLine("| Member | Lines | Name flag | Callers (namespaces) | Ownership hint |");
        sb.AppendLine("|--------|-------|-----------|----------------------|----------------|");

        var summary = new Summary();
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var ownership = member.Judge(callers[i]);
            summary.Add(member.Name, member.Lines, ownership);

            var lenCell = member.Lines > 15 ? $"**{member.Lines}**" : member.Lines.ToString();
            var callerCell = callers[i].Count == 0 ? "(none/internal)"
                : string.Join(", ", callers[i].Distinct().Select(Concept.Short).Take(4));
            sb.AppendLine($"| {member.Name} | {lenCell} | {member.Name.Flag} | {callerCell} | {ownership.Verdict} |");
        }

        sb.Append(summary.Footer());
        return sb.ToString();
    }
}
