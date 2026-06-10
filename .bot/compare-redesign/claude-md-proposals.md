## architect — compare-redesign — 2026-06-08
**Target:** /CLAUDE.md (Runtime2 Conventions)
**Why:** Ingi asked for a consistent notation when referring to class members in docs/plans/comments, applicable to all classes: an instance call written as `Data.Compare(...)` reads as a static and misleads. The fix is free and removes the ambiguity at a glance. Surfaced while writing the comparison-redesign stages (`Data.FromRaw` is static; `Compare`/`Value`/`Peek` are instance).
**Proposed change:**

```
- **Member-reference notation (docs/comments).** When naming a class member in prose, the leading token signals static vs instance: a **static** member is written on the capital Type (`Data.FromRaw`, `Path.Resolve`), an **instance** member on a lowercase instance variable (`data.Value()`, `data.Compare(other)`, `path.ReadText()`). Applies to all classes. This is distinct from a namespace-qualified type name (the namespace stays lowercase: `app.data.IEquatableValue`) and from a type used in a signature (`Compare(Data other)` — `Data` is the parameter type). The rule is only about member-access expressions.
```

## coder — speed-workflow — 2026-06-10
**Target:** /CLAUDE.md (Build section)
**Why:** Measured: `dotnet run --project PLang.Tests` costs 90s+ per invocation (restore+eval+build+run); analyzers cost 17s per test-project compile; bots were burning ~5min per edit-test iteration. `./dev.sh` (repo root, added on compare-redesign) encodes the fast path: 1-5s builds, 3s filtered tests, analyzers-on gate before commit.
**Proposed change:**
```markdown
## Build & test speed (MANDATORY workflow)
- Use `./dev.sh` for all C#/plang build+test iteration: `./dev.sh warm &` once at session start; `./dev.sh build` after edits (1-5s incremental, analyzers off); `./dev.sh test <ClassFilter>` (~3s); `./dev.sh ptest` for plang tests; `./dev.sh full` BEFORE COMMIT (analyzers ON — this is the PLNG001/PLNG002 + TUnit-warning gate — plus both suites).
- NEVER `dotnet run --project PLang.Tests` (90s+ per call) and never `dotnet test`. Run the built binary directly: `PLang.Tests/bin/Debug/net10.0/PLang.Tests --treenode-filter "/*/*/*Name*/*"`.
- Never hand-mix analyzer flags between builds — a flag flip invalidates MSBuild incremental state (full rebuild). The script keeps flags consistent.
```
