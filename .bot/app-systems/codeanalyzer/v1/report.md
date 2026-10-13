# codeanalyzer v1 — app-systems pre-merge OBP scan

Scope, method, and carry-discipline in `plan.md`. Range: `0ea5a4b94..app-systems`. Read-only; a fix touching a locked surface is a **proposal**, never an edit.

Each finding carries: smell name · file:line · what's wrong · fix direction · **blocks / carry** · **surface change: yes/no**.

---

## Area 1 — data and the wire (`PLang/app/data/`, `data/Wire.cs`)

**Diff:** +1,843 −3,658 across 34 files. Net deletion — a large simplification. Gone this branch: `Compare.cs`, `IEquatableValue`, `IListLeaf`, `ILoadable`, `IOrderableValue`, `ScalarComparer`, `ShouldExit`, `TString`, `TypedValueNode`, `WireLocal`, `this.Load.cs`, `this.Normalize.cs` (−276), `this.Reconstruct.cs` (−312), and 586 lines off `Wire.cs`. New: `Comparison.cs`, `Operator.cs`, `OutputException.cs`, `reader/`, `schema/` (registry + `ISchemaReader` + `signature`), `this.Output.cs`.

**Tooling:** `git diff` per file + full read of every touched type + `ObpScan app.data`. ObpScan's MSBuild emits NU-prune/vuln load warnings but renders the member tables.

### Verdict: CLEAN (no merge blockers)

The new surface is OBP-exemplary and the weighted smells present are all already carried. Detail below.

### New surface — reviewed, OBP-correct (no finding)

- **`schema/this.cs` — the `@schema` reader registry.** A `Dictionary<string, ISchemaReader>` keyed by `Schema`, each reader owning its behaviour (`data` reads the envelope, `signature` verifies + peels). Selection on the registry, behaviour on the element — the correct shape, not a fork. Unknown schema throws loudly (registry miss). Mirrors `app.type.reader.@this`.
- **`Operator.cs` — the condition engine (relocated into `data`).** One `Registry` dict mapping operator string → async evaluator; negation derived once via `Negated(positive)` (no flag flips a step's logic); equality/ordering routed through the value's own `Compare` → `Comparison`. "Adding an operator = one entry." No per-type switch, no fork. `IsType` (:163-186) follows the ref, narrows, answers from the chain — this is the **correct** side of the carried json-clr-leak (the bug lives in the json kind, not here).
- **`reader/this.cs` — the `@schema:data` reader.** Streams `{name,type,value,properties}` off the `IReader`; `switch(key)` is legitimate wire-key reading. Lazy `source` value slot, no DOM. Clean.
- **`Comparison.cs` — the sign-free comparison enum + `IncomparableException` + `Invert`.** Enum-not-int is deliberate and documented (a magic `-2` would satisfy `<0`). `IncomparableException` is **used** (thrown/caught across the sync `IComparer` boundary in `type/item/list/this.cs:530/942`), not dead. ObpScan's "MISPLACED" on `Invert` is a false positive (extension on the enum it extends).
- **`this.Output.cs` — "Data writes itself".** Single async pass, each item emits its own form; bounded self-ref recursion guard throws a typed error (not a stack overflow). View-mode read (`mode == View.Store`), not a value type-switch. Clean.

### Weighted smells present — all CARRIED (cite, not re-raised)

1. **exception-as-answer — the set-child bridge.** `this.Navigation.cs:51-63`: `item.Set` throws `NotSupportedException`, `data.Set` catches it to answer `CannotSetChild`. → **carried:** next-branch.md "The set-child bridge (59's remainder)" — removing it changes item's locked surface (Ingi's). **Carry. Surface change: yes.**
2. **reader throws its refusal.** `schema/signature.cs:83,89` throw `DeclinedException`; `reader/this.cs:64,82,102` throw `JsonException`/`UntypedValueException`/`PrFormatOutdatedException` for a stale/invalid `.pr`. → **carried:** next-branch.md "A serializer Reader throws its refusal (714)" — `IReader` can't answer an error; changing it is a serializer-surface change (Ingi's). **Carry. Surface change: yes.**
3. **clr leak on structured json.** `Operator.IsType` encodes the ruled promise (read→narrow→answer) that `type/item/kind/json/this.cs` violates by holding parsed json as `clr(JsonElement)`. → **carried:** next-branch.md "The {item, json} door (717)". **Carry. Surface change: yes** (the json kind parsing into native dict/list).

### Minor findings (cleanup — do not block the merge)

- **F1.1 — dead const (deletion test).** `this.Output.cs:21` `private const int MaxNormalizeDepth = 128;` is referenced nowhere (grep: 1 hit, the declaration). It mirrors a cap for the `this.Normalize.cs` tree that was **deleted this branch**; the live Output path uses `MaxResolveDepth` (:27). Delete lines 16-21 — nothing breaks. **Carry (trivial; leave for coder). Surface change: no.**
- **F1.2 — borderline: `IsDataMarked` placement.** `this.cs:56` is a `static bool IsDataMarked(JsonElement)` on `data.@this`, called only by the json leaf serializer (`item/serializer/json.cs:94,187`). It's a legitimate wire-shape probe (does this JSON carry a `@schema` marker), and a leaf serializer is allowed to read JSON — but it reads as a stray helper that could live on the json serializer beside its one caller. **Carry (note, not a fix). Surface change: no.**
- **F1.3 — borderline: `JsonString.ToJson` parse-ladder.** `JsonString.cs:30-91` drives control flow through 9 `catch (JsonException){}` arms (try strict → fix controls → quote barewords → slice prose → repair truncation → strip commas), returning a clean `Error` only when all fail. This is exception-as-control-flow, but it is a **relocated** pre-existing utility (`App.Utils.Json`, stage 27), not new on this branch, and `JsonNode.Parse` offers no `TryParse`. Not exception-as-answer (each arm is "try next strategy," not a yes/no on a semantic result). **Carry (no action; noted for completeness). Surface change: no.**

### Noise explicitly cleared (so a re-scan doesn't re-chase it)

- ObpScan name-flags on `QuoteBarewordKeys` / `AppendUnmatchedCloses` / `FixJsonStringValues` / `StripTrailingCommas` (JsonString private helpers), `CompareElements/Objects/Arrays` (this.Diff.cs recursive JsonElement walkers — mechanical, localized, diff-util by design), `ReadCore`, `SetValueDirect`: accurate descriptive names / inherent length, not smells.
- `ValidateKind` / `RequireStrictKind` / `CheckStrictKind` (IKindValidatable, IStrictKindEnforcer): **unchanged pre-branch** — not in the `0ea5a4b94..app-systems` diff. Out of scope.
- `Properties.EnsureSupportedValue` (:142-162) primitive `is` list: a wire-boundary accept/throw gate (not a behavioural fork), honest about its best-effort limits inline.
- `code/Default.cs:61` `catch (ArgumentException)`: translates `Regex`'s unavoidable compile-throw into a typed `InvalidPattern` error — a boundary translation, not a swallowed yes/no.

---

*Areas 2-8 pending (channel/stream, core value types, type system, serializer, call/variable/events, module registry/app, builder pipeline).*
