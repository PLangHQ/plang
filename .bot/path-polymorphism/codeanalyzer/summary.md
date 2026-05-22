# codeanalyzer — path-polymorphism

**Version:** v2

## What this is

`path-polymorphism` makes PLang's `path` type scheme-polymorphic: an abstract
`path` base with `FilePath` / `HttpPath` subclasses, a per-App scheme registry,
collapsed file handlers, and the deleted `System.IO.Abstractions` wrapper layer.
codeanalyzer reviews the C# for OBP compliance, simplicity, and silent-failure
risk.

## What was done

**v1** reviewed the initial branch — verdict NEEDS WORK, 8 findings (F1 High,
F2–F4 Med, F5–F8 Low). Headline: handlers downcast to the concrete `filepath`
type (F1) and the base carried file-only semantics broken for `HttpPath` (F2).

**v2** (this version) re-reviewed coder v3's response (commit `eb85fcbd`).
All eight v1 findings are **genuinely fixed** — verified, not suppressed:

- F1 — option-bearing verbs lifted onto the abstract base; handlers are
  one-liners; zero `is filepath` downcasts remain tree-wide.
- F2 — `Exists`/`Size` moved to `FilePath`; base carries only address props.
- F3 — `IBooleanResolvable` + async condition/assert pipeline; `file.exists`
  returns `Data<Path>` uniformly.
- F4 — `As<T>.InvokeResolve` catches a thrown `Resolve`, returns failed `Data`.
- F5–F8 — `RootComparison` in `Relative`, `OsAbsolutePath` on `app`, attribute
  doc fixed, `HttpPath.List/Mkdir` route through `AuthGate`.

Build clean (0 errors). C# 2881/2881 pass. plang 201/203 pass — the 1 fail is
an external `httpbin.org` 502/503 outage (not code); 1 stale is pre-existing.

**Verdict: NEEDS WORK** — a small one. Three new findings, one substantive:

- **N1 (Med)** — the F3 refactor silently dropped `file.exists`'s `Read`
  authorization gate. Old `ExistsPathAsync` gated; new `file.exists` is a pure
  identity passthrough and `FilePath.AsBooleanAsync` deliberately skips
  `AuthGate`. Out-of-root file-existence probing is now ungated. Worse, it's
  asymmetric: `HttpPath.AsBooleanAsync` *does* authorize. A permission gate
  changing by side effect needs an explicit, recorded decision.
- **N2 (Low)** — `path.Equals`/`GetHashCode` still hard-code `OrdinalIgnoreCase`;
  same drift F5 fixed in `Relative`. Use `RootComparison`.
- **N3 (Low)** — assert's `ResolveTruthy` re-implements the `IBooleanResolvable`
  dispatch instead of reusing `Data.ToBooleanAsync()`.

## What to do next

Coder addresses N1 (decide: restore the gate, or confirm + document ungated
file-existence). N2/N3 can ride along. Full detail: `v2/report.md`.

## Code example

The F1 fix — the abstract base now carries the *option-bearing* verb surface so
handlers never downcast:

```csharp
// types/path/this.Operations.cs — base
public abstract Task<data.@this> Delete(bool recursive, bool ignoreIfNotFound);
public Task<data.@this> Delete() => Delete(false, false);   // convenience

// modules/file/delete.cs — handler collapses to a one-liner
public async Task<data.@this> Run()
{
    if (!Path.Success) return Path;          // F4 — typed scheme error, not NRE
    return await Path.Value!.Delete(Recursive.Value, IgnoreIfNotFound.Value);
}
```
