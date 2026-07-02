# Behavior lives on its owner (OBP hardening — the non-output residue)

## Why

An operation belongs on the type that owns the data it acts on. When it lives somewhere else — a courier reading `Data.Value`, a registry type-switching over subtypes, a handler decomposing a value into primitives for a static helper — you get two failure modes we have already swept as separate problems: **duplicate implementations** (the same logic copied across call sites) and **null-guessing** (a relay branching on a value it shouldn't have opened). Duplicate execution paths and nullable objects are downstream symptoms of behavior not sitting on its owner. This plan moves the residual off-owner behavior back home.

The payoff is disproportionate for a small sweep. The tree is already largely clean on this axis — registries dispatch to per-type hooks by reflection, and comparison / enumeration / sort / truthiness route through virtuals on the value. What's left is about ten spots, and several of them are the *root* of findings the two earlier scans flagged as symptoms (see "Why this is upstream"). Fixing the owner retires the symptom.

**Scope fact you must read first:** the value-**output** application of this exact invariant is already owned by context-never-null's coder track — `.bot/context-never-null/coder/output-resolution-unification-plan.md` sets the rule "**`Data.Output` contains ZERO `_item is <Type>` questions**" and claims the json dict/list writer, the `output.write` / `mock.intercept` template blocks, the plang signature-layer switch, and the ref-detector consolidation. This branch is the **same invariant applied to every other surface** that plan does not touch. The overlap is deferred, not duplicated — see "Scope boundary" below.

## The invariant (shared with the output work)

> An operation belongs on the type that owns the data it acts on. A relay, courier, registry, or wire layer never type-switches on the content it carries — it delegates to a virtual on the element, and the element answers for itself.

This is three rules already in `CLAUDE.md` applied as one codebase sweep: Rule #9 (only leaves touch `Data.Value`), the registry-type-switch smell ("a type-switch inside a registry is misplaced behavior; push it onto the element"), and Rules #2/#8 (don't decompose a value into primitives at the call site — pass the whole carrier). context-never-null applies the invariant to `Data.Output`; this branch applies it everywhere else.

## The three faces

- **Face 1 — courier reads the value.** A relay reads `Data.Value` (`data.Value as X`, `.Value is X`, `.Value switch`, or passes `.Value` onward) to branch on or operate on the contained value. Allowed: a leaf reading its own value — a handler declaring a typed `Data<T>` parameter, or the value's own per-(type,format) serializer file. Violation: a relay opening the package mid-flight.
- **Face 2 — type-switch instead of virtual.** Behavior branched over element subtypes (`is X`, `switch` on runtime type, if/else on kind) where the branch belongs on the element as a virtual. Allowed: pattern-matching at a true boundary (parsing raw LLM/HTTP/JSON/CLI input, dispatch on a CLR type that isn't ours). Violation: a switch over our own subtypes each type could answer.
- **Face 3 — decompose at the call site.** `await X.Value()` (or reading `a.Field`) on an operand only to pass the raw inside to a static/free helper — `Op(a.Value, b.Value)`. Call the op on the carrier, pass operands whole — `a.Op(b)`. Applies even at a leaf (a leaf may read its own value but must not chop it into primitives for a helper).

## Scope boundary — what belongs to context-never-null, not here

Deferred to `output-resolution-unification-plan.md` (do **not** re-plan or touch these here):

- The json writer special-casing `dict`/`list` (`channel/serializer/json/writer.cs:153-177`) and the `json/writer.cs:BeginRecord` parallel envelope — its duplications #5/#6.
- The `output.write.Run` and `mock/intercept` template-resolve blocks (`output/write.cs:21`, `mock/intercept.cs:128`) — its §4, collapsing to `channel.write(data)`.
- The plang serializer's `is signature.@this` layer switch and any `_item is <Type>` in `Data.Output` — covered by its zero-`_item is` invariant.
- The "is this a ref?" detector consolidation — `HasVariableReference` / `IsVariable` / raw `Peek() is variable` — its duplication #4.

**Consequence for this branch:** the one finding that touches that seam — `data.Detach` (move #2) — must *consume* context-never-null's consolidated ref-detector, not re-introduce a `Peek() is variable.@this` type-test. Build move #2 on their `IsVariable`/`HasVariableReference`, whichever survives their consolidation.

## Why this is upstream (convergence with the two prior scans)

Three moves here are the cause of symptoms the earlier scans reported downstream:

- **Error wire-extras (move #1)** is the `ErrorWire`-vs-leaf divergence the duplicate-paths scan flagged — and it is a **live bug**: the two subtype switches have drifted and neither persists `AssertionError` / `ActionError` / `ValidationError` / `SettingsError` / `CallbackGoalErrors` fields, so they are silently dropped from the wire.
- **`data.Detach` (move #2)** is the "same snapshot logic in two relay files" the duplicate-paths scan saw — one owner method retires the duplicate.
- **Bool truthiness (move #3)** is the exists-probe transform the duplicate-paths scan found repeated per subsystem.

## The worklist — bug first, then by leverage

Each move as a leaf trace: **Behavior / Incumbent / Owner / Move / What dies.** Code shapes and names are suggestions — see "You own this."

### Tier 0 — the bug

**1. Error subtypes own their wire fields (Face 2).**
- Behavior: (de)serialize error subtype-specific fields (`AskError`, `PermissionDenied`, `AssertionError`, `ActionError`, `ValidationError`, `SettingsError`, `CallbackGoalErrors`).
- Incumbent: two hand-maintained subtype switches — `error/serializer/Default.cs:35` (`is AskError`) and `error/IError.Wire.cs:77-88` (write) + `:109-117` (read).
- Owner: each `IError` subtype. The correct pattern already exists on the same class — `Format()` dispatches subtype extras through the `FormatExtra` virtual (`error/Error.cs:419`, overridden by `ActionError`/`AssertionError`/`SettingsError`).
- Move: add a symmetric wire-extras virtual mirroring `FormatExtra` — each error writes and reads its own fields. Delete both switches.
- What dies: the two switches, and the drift bug (fields dropped on the wire). **Treat as a bug fix, not a refactor** — data is being lost today.
- Coordinate: these files are also touched by context-never-null Stage 4 (serializer binds context). The two changes are orthogonal (context threading vs field ownership) but hit the same files — sequence after Stage 4 lands, or expect a merge reconcile.

### Tier 1 — collapses a prior-scan symptom

**2. `Data.Detach(name, context)` — snapshot a live reference (Face 1).**
- Behavior: before storing a value into a new scope, materialize it if it holds a live variable reference (so later mutations don't change the stored value).
- Incumbent: duplicated — `this.cs:592-596` (`RunGoalAsync` param injection) and `goal/steps/step/actions/action/this.cs:285-286` (`%!data%`), both `Peek() is variable.@this` then `await Value()`.
- Owner: `data.@this` (already half-owns this via `HasVariableReference`, `data/this.cs:163`).
- Move: one method on `Data` that returns a materialized snapshot when it holds a live reference, else `this`. Both call sites become `await param.Detach(param.Name, context)`. **Build on context-never-null's consolidated ref-detector; do not re-test `Peek() is variable`.**
- What dies: the two `Peek() is variable` courier copies.

**3. Read a bool result through the value's own door (Face 3).**
- Behavior: test whether an existence/bool result is true.
- Incumbent: `(await X.Value())?.Value != true` at `goal/Methods.cs:16`, `goal/setup/this.cs:52`, `goal/list/this.cs:347`, `this.cs:407` (~12 repo-wide).
- Owner: `Data` / `bool.@this` — `Data.ToBooleanAsync()` / `IBooleanResolvable` already exists.
- Move: call the existing truthiness door (`await exists.ToBooleanAsync()`), never decompose to `.Value`. **No new surface** — the door is already there; this is deleting the decomposition. (Do *not* add an `IsTrue()` — see OBP validation.)
- What dies: the identical `(await X.Value())?.Value` transform at every site (OBP smell #5).

### Tier 2 — clean single-owner moves

**4. `StartsWith(Data)` / `EndsWith(Data)` virtuals on `item.@this` (Face 3).**
- Incumbent: `condition/Operator.cs:160-168` — `startswith`/`endswith` do `StringOp(await Val(l), await Val(r))` → `.ToString()` + a static helper.
- Owner: `item.@this`. Asymmetry is the tell — `contains` already routes through `lv.Contains(right)` (virtual on `item.@this`); these two don't. Confirmed `item.@this` has `Contains`/`IsEmpty` but not `StartsWith`/`EndsWith`.
- Move: add the two virtuals (mirror `Contains(Data)`, pass the whole carrier); a `path`/`url` then answers for its own text face instead of defaulting to `ToString()`.

**5. Virtual typed/text read on `channel.@this` (Face 2).**
- Incumbent: `channel/list/this.cs:196` (`ReadChannelAsync<T>`), `:220` (`WriteTextAsync`), `:240` (`ReadTextAsync`) — the registry type-switches `is channel.type.stream.@this` to reach `.Stream`.
- Owner: `channel.@this` base; `stream` overrides.
- Move: virtual read/write on the base — the registry stops switching on element subtype.

**6. `archive.Compress()` / `Decompress()` on the type (Face 1 + Face 3).**
- Incumbent: `data/this.Transport.cs:125-139` — Data peeks the archive value, reads `.Value`/`.Algo`, hands raw bytes to a free static `GZipDecompress`.
- Owner: `app.type.archive.@this`. The file's own TODO (`this.Transport.cs:64`) already says compression belongs on an archive owner.
- Move: `await archive.Compress()` / `Decompress()` on the carrier.

**7. "Persist bare content" on `item.@this` (Face 1 + 2 + 3, one block).**
- Incumbent: `type/path/file/this.Operations.cs:227-245` — `Save` opens the payload, switches `is binary` / `is text`, and hands the raw bytes/string to `File.Write*`.
- Owner: `item.@this` — `binary`/`text` already own their bare form via `Write(IWriter)`.
- Move: prefer routing `Save` through the value's existing bare-write rather than inventing a hook; the `is binary`/`is text` switch and the decompose disappear together.

**8. Hand the whole Data to `target.Write(prop, dv)` (Face 1 + 2 + 3, one line).**
- Incumbent: `variable/list/this.cs:329-331` (`SetChildOnPath`) — switches on the *target's* type (`target is dict/list ? Peek() : await Value()`) to decide how to unwrap the incoming Data.
- Owner: the target value (`dict`/`list`/CLR slot).
- Move: pass the Data whole into `target.Write(propertyName, dv2)` — the delegation seam already exists ten lines below (`:340`). The container stores the lazy binding; a scalar slot resolves. Removes the target type-switch and the pre-decompose.
- Note: adjacent to context-never-null's variable work — coordinate on `variable/list`.

**9. Generator emits `.Peek().IsNull`, not `is @null.@this` (Face 2).**
- Incumbent: `PLang.Generators/Emission/Property/Data/this.cs:95` — every `[Default]` handler gets `{Backing}.Peek() is app.type.@null.@this`.
- Owner: the value — `item.@this.IsNull` already exists (used at `Action/this.cs:221`).
- Move: emit `.Peek().IsNull`. One-token change, but replicated into every defaulted handler — high fan-out. Same invariant as context-never-null's "no `_item is <Type>`", applied in the generator.

### Tier 3 — mechanical / borderline

**10. `math/*` static ops → instance methods on `number.@this` (Face 3).**
- Incumbent: 14 handlers (`math/add.cs`, `subtract.cs`, `round.cs`, `abs.cs`, `min.cs`, `max.cs`, …) call static `number.Add(an, bn, policy)`, `number.Round(n, decimals)`, etc.
- Owner: `number.@this` — the ops already live there, as statics (`this.Arithmetic.cs` / `this.Unary.cs`).
- Move: convert static → instance (`an.Add(bn, policy)`, `n.Round(decimals)`). Also drop the unused `NumberPolicy` on `Min`/`Max` (`this.Unary.cs:20-21`).
- **Caveat — lower urgency than the CLAUDE.md citation implies:** the handlers already pass whole `number` carriers (not raw primitives), so this is the *static-helper* variant, not a decompose bug. Mechanical, identical across all 14 sites. Do it for consistency, rank it by effort.

**Borderline (record, decide during implementation):**
- `crypto.Hash`'s `is binary.@this → bin.Value` shortcut (`crypto/code/Default.cs:24,31-33`) — a polymorphic hasher opening the box for one subtype. Its own TODO points at routing through `data.Output`. Overlaps context-never-null's Store-view canonicalization — coordinate; likely folds into their work.
- Callstack stamping `Error.Params`/`CallFrames` from outside after `result.Error is Error` (`callstack/call/this.cs:237-258`) — also an OBP-#1 public-mutable-field smell on `Error`. Needs a shape *and* naming pass (not `AttachDiagnostics` — see OBP validation).
- The builder's LLM-plan type-switches (`builder/code/Default.cs:759-770`, `790-894`) — mostly excused: they sit at the raw-LLM-output parse boundary. Only worth moving once the planner output is parsed into a typed step at the boundary.

## What stays (the stays-list — do NOT "fix" these)

These are the correct patterns; touching them would be a regression.

- Registries dispatching by reflection to per-type hooks: `type/catalog/**`, `type/convert/**`, `type/compare/**`, `type/renderer/**`, `type/reader/**`, `kind/Hooks.cs`. Exemplary — `convert.OwnerOf` composes routing from each family's own `OwnedClr`.
- Per-type `this.Convert.cs` / serializer / reader hooks — the value's own construction/rendering (leaves).
- Parse boundaries switching on CLR/JSON/token shape at the perimeter: `catalog/Conversion.cs TryConvert`, the json/text readers (`JsonValueKind`/`TokenKind`), CLI/`--test`/`--config`/actor-name parse, HTTP body shaping, LLM request/response mapping, Roslyn `TypedConstant`.
- `condition/Operator.cs` core — `==`/`!=`/`<`/`>` route through `left.Compare(right)`, truthiness through `ToBooleanAsync()`, `contains`/`in` through `Contains`, `is` through `left.Type.Is(...)`. Only `startswith`/`endswith` (move #4) are off-owner.
- `assert/code/Default.cs` — routes through `expected.Compare(actual)` / `ToBooleanAsync()` / `Contains`.
- `list/*` — `sort`/`add`/`contains`/`group`/`unique` route through `list.@this` virtuals; `list.LeafCount` deliberately avoids an `is list` switch via `IListLeaf.LeafCount` (the pattern done right).
- `ShouldExit` — `d.Peek() is IExitsGoal → eg.ShouldExit()`: the correct marker-interface + virtual pattern.

## OBP validation of the new surfaces

Applying the verb+noun and object-decomposition checks to every member this plan proposes:

| Proposed surface | Check | Verdict |
|---|---|---|
| `item.StartsWith(Data)` / `item.EndsWith(Data)` | single verb, whole carrier | clean — mirrors `Contains(Data)` |
| `archive.Compress()` / `Decompress()` | single verbs | clean |
| `channel.Read<T>()` / `ReadText()` / `WriteText()` | single verbs, whole | clean |
| `number` instance ops (`Add`/`Round`/`Abs`/`Min`/`Max`) | verbs, whole carriers | clean; drop the dead `Min`/`Max` policy param |
| `Data.Detach(name, context)` | single verb; `name` names the binding (like `IName`) | clean-ish — confirm `Detach` reads right; naming is coder's |
| bool truthiness | — | **no new surface** — use existing `ToBooleanAsync()`; do NOT add `IsTrue()` (property-shaped `Is`+adjective) |
| `crypto` canonical bytes | `CanonicalBytes` = adjective+noun, property-shaped | **flagged** — route through existing `data.Output` canonical-write; if a method is unavoidable, `Canonicalize(view)` (verb) |
| error diagnostics stamp | `AttachDiagnostics` = verb+noun compound | **flagged** — rename; prefer the error capturing its own frames at construction so the courier stamps nothing |
| error wire-extras virtual | mirror existing `FormatExtra` | clean — follow the established precedent's shape |

Self-audit outcome: three proposals carried a smell (`IsTrue`, `CanonicalBytes`, `AttachDiagnostics`); all three resolve by using an existing door or a single-verb rename. The rest pass.

## Cross-check vs context-never-null demolition

This branch forked from context-never-null's head, so it carries that branch's plan docs but not its (still-pending) code changes. Shared surfaces to reconcile when both land:

- **Error serializers** (`error/serializer/Default.cs`, `error/IError.Wire.cs`) — Stage 4 makes them context-ful; move #1 makes subtypes own their fields. Orthogonal, same files. Sequence #1 after Stage 4 or expect a reconcile.
- **`Data.Output` / value rendering / `%ref%` resolution / json writer** — entirely context-never-null's (`output-resolution-unification-plan.md`). Not touched here.
- **`variable/list`** — their Output/ref-detector work vs move #8's `SetChildOnPath`. Different methods; coordinate.
- **Hashing/Store canonicalization** — the `crypto.Hash` borderline likely folds into their Store-view work; defer to them.

Rule for the coder: never re-add a relay type-switch that context-never-null's zero-`_item is` invariant removes. If a finding here starts to look like value-output rendering, it's theirs.

## You own this

Every file:line, method name, and code shape above is a finding and a suggestion — the coder owns the final names, shapes, and sequencing. Two fixed points: **move #1 is a bug** (error fields are being dropped on the wire today — treat it as a fix, land it first, and it should carry a regression test), and the **scope boundary is a contract** (value-output belongs to context-never-null; this branch is everything else). Staging comes after this plan is reviewed.
