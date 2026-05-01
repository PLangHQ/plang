# codeanalyzer v4 — review coder/v7 (Variable + IRawNameResolvable carve-out)

## What changed since codeanalyzer/v3

coder/v7 retired the `[VariableName]` attribute and the parallel
`__Resolve<T>` / `__StripPercent` / `__HasParam` helper family. The
replacement: `App.Variables.Variable` (a record carrying `Name`, `RawValue`,
`WasPercentWrapped`) used as `Data<Variable>` on every former
`[VariableName] string` slot. PLNG001 collapses to a two-rule gate:
`Data<T>` (or plain `Data`) or `[Provider] T`.

The architect's plan asserted the existing `Data.As<T>` static-Resolve
dispatch (line 612-624) would route `Data<Variable>.As<Variable>(ctx)` to
`Variable.Resolve`. Empirically false for `%x%` slots — TryFullVarMatch
intercepts first. coder/v7 added an **IRawNameResolvable marker carve-out**
to `Data.AsT_Impl` (lines 544-562) that runs BEFORE the `%var%` substitution
branch when `T : IRawNameResolvable`. Path is unaffected (doesn't implement
the marker).

22 handler property declarations migrated, 1 attribute deleted, 1 file
deleted, 5 PLNG001PostMigration tests activated.

## Review surface (per coder/v7 hand-off)

1. `Data.AsT_Impl` carve-out — placement, cache reuse, raw-name dispatch.
2. `Variable` — three-field record + single-arg helper ctor + ToString
   override. Equality stays default (architect's documented decision).
3. `[VariableName]` removal — any remaining reflection or string references?
4. 22 handler migrations — Pattern A (write target) vs Pattern B (read by
   name) consistency; nullable `loop/foreach.cs` variant.
5. PLNG001 simplification — two-rule gate.
6. Generator simplification — `Legacy/this.cs` deleted, helper family
   pruned, comments updated.

## Five passes (per character file)

- **Pass 1 — OBP Compliance**: Folder shape, `@this` convention, alias usage,
  generator structure.
- **Pass 2 — Simplification**: Dead/duplicated code, redundant predicates,
  stale comments.
- **Pass 3 — Readability**: Naming, cohesion, comment clarity around the
  carve-out.
- **Pass 4 — Behavioral Reasoning**: Trace data origins through carve-out;
  cycle/depth interaction; null/empty edge cases; clone/copy audit on
  Variable; latent NPE in implicit conversion.
- **Pass 5 — Deletion Test**: For each new line — does a test fail without
  it? `WasPercentWrapped` is a particular candidate (architect notes
  "future build-time validators" — currently no consumer).

## Outputs

- `result.md` — per-file findings with line references.
- `verdict.json` — pass/fail with one-line summary.
- `summary.md` — this version's summary.
- Bot-root `summary.md` — append v4 entry.
- `report.json` — append session entry.
