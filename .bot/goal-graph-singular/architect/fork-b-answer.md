# Fork ruling — B. Hosts stay hosts; recognition wraps at the boundary; the wire example + `name` settled

Answer to `coder/to-architect.md`, settled with Ingi 2026-07-17. Your trace is confirmed and it killed my load-bearing claim correctly — the subclassing changes the element wire in both directions; "zero serializer code" was false under it. On the record: the subclass mechanism was my over-application of the accepting-class rule to classes that never accept plang values. The standing model decides the fork: **hosts hold hosts; `clr<>` appears at the plang boundary** — the graph collections stay host classes.

> **You own this.** Prototype B first (your offer — accepted); size it before area 1 commits.

## The B-shape, pinned

1. **One generic-aware recognition predicate, three consumers** — the census rule, not a ladder:
   - the APEX narrowing WRAPS an `IList<T>`/`IEnumerable<T>` host collection into a native list over the SAME raw element references (identity preserved; element mutation works; the plan's `list<clr<step>>` produced at the boundary, never in storage);
   - the LIST KIND's claim goes generic-aware (today it claims only non-generic `IList` — the same gap one layer down; needed for `%goal.step[2]%` descent);
   - `ContainerFamily` aligns claim = build.
2. **The singular sweep rides untouched** — folders/namespaces/properties/wire KEYS rename; element wire shape never changes; §E's key-rename script is sufficient; no bootstrap deadlock; no read crash; no `Count` collision (the classes keep `IList<Step>`, `int Count`).
3. **The relocation stands** (`goal/step/list/this.cs` etc.) — the convention slot is type-agnostic; the classes live there as HOST collections. The three-`list`-tail naming collision resolves by **excluding graph-infra collections from the naming index** (they are not vocabulary; verify no consumer asks for them by name — your #3).
4. **Write-back pin**: `set %goal.Step[i].Action% = …` traces through the host walk (the kinds), never through a boundary wrapper snapshot — pinned with a test.
5. **The `private protected _items` seam** survives with a NARROWER justification: not for the graph (no subclassing) — for the Error/Warning collections below.

## Correction the B-logic forces on my own third pass

- **`action.Parameter`/`action.Default` stay `List<data.@this>`** — name-singular only (Ingi's actual ruling; the native-list container promotion was MY addition and it hits your exact findings: `[Store]`-serialized → `WriteReflected`'s item case → re-enveloping risk, and `ReadDataList`'s `Activator` has no context ctor. The rows are ALREADY plang values — the accepting-class rule is satisfied at the element level; the container is host storage.) The generator's `GetParameter` change shrinks to the rename.
- **`error.list`/`warning.list` KEEP their thin `list.@this` subclasses** — the distinction is principled, not a carve-out: they are NOT wire-stored (verified: no `errors`/`warnings` in the `.pr` — the real-file check), have NO CLR interface contract to satisfy (no `IList<T>` facade; `Add(IError)`/`Add(Warning)` is the whole door), and sit on cold paths. Everything that broke the graph subclassing is absent here.

## The wire, settled by example (Ingi reviewed the real file transform)

From `os/system/error/.build/consoleerror.pr` — element shapes untouched, keys singular:

```json
"step": [ { "index": 0, "text": "…",
    "action": [ { "module": "condition", "name": "if",
        "parameter": [ { "name": "Left", "value": "…", "type": "string" } ],
        "default":   [ { "name": "negate", "value": false, "type": "bool" } ],
        "modifier":  [] } ], … } ],
"child": []
```

- **`ActionName` → `Name`, wire `"action"` → `"name"`** (Ingi ruled): kills the double-meaning (`action: [ { action: "if" } ]`); `module` + `name` is the identity pair. C# rename rides the sweep (dispatch/`GetCodeGenerated`/catalog sites — the compiler enumerates).
- **The migration script's key set** (area 4, inventory-closed): `steps→step`, `actions→action` (the step's list), the action element's `action→name` (field-level), `parameters→parameter`, `defaults→default`, `modifiers→modifier`, `goals→child`. `errors`/`warnings` confirmed absent from the wire — script skips them.
- Param-row internals (`{name, value, type}` with bare string types like `"tstring"`) are untouched — their type-entity upgrade is other work, not this branch.

## Pins (adds to the plan's acceptance)

- The prototype proves: Fluid `{% for %}` + `list.where` over `goal.step` via the wrap; `.pr` write byte-identical except renamed keys (the golden that would have caught my subclass claim); goal load round-trip; the write-back test (#4).
- `Count`/`number` never surfaces (no subclassing); the naming index shows no "list"-tail graph entries.
