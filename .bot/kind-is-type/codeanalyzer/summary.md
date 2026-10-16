# codeanalyzer — kind-is-type

**Version:** v1

## What this is

The OBP code review of branch `kind-is-type` (decision 774, *a kind is a type*) before it carries into runtime2. The branch reshapes the locked core so a kind is a type under its parent (`kind<T> : kind : type`), settings become their own top-level types that read themselves back from their stored record, and a goal call's name can't climb out of its folder. The architect routed the **landed** work — **stages 0–3**, plus **decisions 767 and 789/790** — for review now; stage 4+ (json-owns-its-bytes, the navigator deletion, the builder) is still mid-flight and reviewed on the architect's go.

## What was done

Reviewed in the codeanalyzer's passes: every core type read in full (not only the diff), callers walked upstream, a compiler-backed hide sweep, and targeted greps for the model's own OBP gates. `v1/report.md` is the artifact of record; `v1/verdict.json` the one-liner.

**Verdict: CLEAN — PASS. No finding blocks the merge. Next bot: tester.**

The kind model holds up against its own OBP gates (`start.md` "OBP validation"):
- **one fallback seam** — `kind<T>` (`Family = T`); a kind overrides only its own, the parent never asks its kind back (type's fact hooks read `OwnX ?? kind.OwnX ?? Family._field`, the stored field, which terminates).
- **no `is kind` behavioural fork** outside `type/kind/` — the two `is kind.@this` hits (`size`/`duration`) are non-null binds where the value re-expresses itself in a requested standard read from its own kind registry.
- **no new app-wide by-name kind search** — the parent-scoped `Kind(type, kind)` is the door; `Held` was kept for MIME/extension only (ruling 2 / 782); the surviving bare-name callers are the deferred text coiner (782, stage 4) and the pre-existing `is`-operator `HasKind` guard (unchanged from base).
- **settings (789/790)** read back from their own record via `Record` + `type.Construct` (class from the key, not a stored kind); old rows decline loudly (`SettingsUnreadable`); the start-up loop and the old-shape reader are gone.
- **767** — `goal/list/this.cs Find` refuses a `..` segment with `GoalNameClimbs` (400) before anything resolves.

Deterministic backing: `dotnet build PLang` **succeeds, 0 errors**, PLNG001/002 clean; the ~20 `CS0108` hides are all **pre-existing** (verified against the runtime2 base and against unchanged files) — the branch introduced **no new hide**, which is what the 776 ruling promised.

### Findings (none blocking; all later-pass cleanup; none a surface change)

| ID | what | file:line | fix |
|----|------|-----------|-----|
| F1 | dead `TypeDescription` const (no reader; teaching lives in `type.description.md`) | `type/this.cs:150–166` | delete; likely pre-existing |
| F2 | class doc narrates removed `BuildTypeEntries`/`Promote` construction path | `type/this.cs:21–26,144–149` | rewrite to the current ctor-fold |
| F3 | `subclass.Offers` reflects `GetProperty("Offer")` per call — a second door to a fact `type.cs:810` reads once via `Declared<T>`, against the branch's bind-once discipline | `type/kind/subclass/this.cs:31–34` | bind `Offer` once at kind creation |

Cited, not re-raised: `WriteReflected` OBPV (todos carry), `IsSequence` `new` on the kind base (acknowledged), `ToString name<kind>` (tracked F4.2, stage 6), the `Clr<object>() ?? value is string` failure idiom (app-systems F3.1 carry), `HasKind` app-wide guard (pre-existing; obp-cleanup candidate). Carry-discipline don't-raise items confirmed present: `type.Construct` beside `Make`, bool's closed pair (782), `Step.Set` json round-trip (798), `template` on its base (782).

## Code example — the model's shape done right (the type's fact hooks)

```csharp
// type/this.cs — a kind answers its own, else the family's STORED field (never the virtual:
// a kind's Family is its parent entry, so the field terminates; the member could loop).
public property.list.@this? Property { get => OwnProperty ?? kind.OwnProperty ?? Family._property; init => _property = value; }
public virtual ValueTask<IReadOnlyList<item.@this>> Offers(step step)
    => Family._offer is { } declared ? new(declared) : kind.Offers(step);
```
One fallback written once; the element owns its behaviour. This is why there is no `is kind` fork and no app-wide kind search — a kind is just the type one level down.

## Environment

Ran in an isolated worktree `/workspace/plang-kind-is-type` on `kind-is-type` — the shared `/workspace/plang` checkout (on `compare-redesign`) must not have its branch switched. `Tools/ObpScan`'s Roslyn `MSBuildWorkspace` loader can't load `PLang.csproj` here (NuGet prune/vuln warnings abort the project model — a known tooling limitation; `dotnet build` is clean), so the member sweep was full-read + grep + the real compiler.

## Next

PASS → **tester**. Nothing routes to the coder as a merge blocker; F1–F3 are later-pass cleanups. Stages 4–6 await the architect's go for a v2 review round.
