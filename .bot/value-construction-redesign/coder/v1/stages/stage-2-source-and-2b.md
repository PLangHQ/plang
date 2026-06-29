# Stage 2 — source absorbs Build's from-raw special cases + pin case-2b's home (additive)

**Goal:** make the two destinations the ctor flip (Stage 3) will target *exist and be correct* before the flip — so Stage 3 is a clean re-route, not a simultaneous build-and-rewire.

1. The **from-raw path** (`source` + reader) must handle the two special cases `Build` currently owns: a Variable-name target (`%s%` write-target) and a `%ref%` template — so that minting a `source` for a raw form loses nothing.
2. The **re-type-a-built-value op** (case 2b) must exist as a named, stable member so Stage 3's fork can call it.

**Kind:** additive. `Build`/`Judge` still exist and still have their callers; nothing is deleted. New behavior lands beside the old.

---

## Step 2.1 — confirm `source` already covers Variable + `%ref%` on the from-raw path

`Build` (`type:232`) handles two cases before its `Convert` call:

```csharp
// Variable-name declared type — %s% NAMES the variable, not a value to render
if (typeof(app.variable.IRawNameResolvable).IsAssignableFrom(ClrType) && backing != null)
    return app.variable.@this.Resolve(backing, Context!);
// %ref% template — never coerce a live template
if (backing != null && text.@this.HasHoles(backing) && value is item.@this template)
    return template;
```

On the **from-raw** path these become `source`'s job. Trace whether the existing read path already covers them:

- **Variable target.** `Data<Variable>` slots use `IRawNameResolvable` → `Data.As<T>` dispatches to `Variable.Resolve` directly (per CLAUDE.md PLNG001 note), and `source`'s reader for a Variable-typed slot should resolve the name. Confirm a `source(raw="s", type=variable)` materializes to the resolved variable, the same as `Build` did. If there is a gap, the fix is in the Variable reader / `source`, **not** the ctor.
- **`%ref%` template.** The reader already borns a `%ref%` leaf as a live template (`source` carries `_template`; `ReadContext` rides the mode — `source.cs:26-28`). Confirm `source(raw="...%x%...", type=text, template=plang)` materializes to a template-backed `text`, not a coerced value.

**Output:** a short trace note in this file (or a test) proving both cases survive on the from-raw path via `source`. If a gap exists, close it inside `source`/the reader additively. Do **not** special-case them in the ctor — that would re-introduce the fork.

---

## Step 2.2 — pin case-2b's home: the convert-a-built-value op

Case 2b = "the declared type converts an already-built item to itself" (`text "5"` → `number 5`). The engine already exists: the 2-arg `type.Convert(object?, ctx)` (`type:177`), which unwraps a built leaf (`leaf.Clr<object>()`) and dispatches to the family hook via `Conversions.Of`. `Build`'s tail (`var built = Convert(value, Context!)`) is the same call.

**Decision to make and record here:** what is case-2b's stable name/home that Stage 3 calls? Options, in OBP preference order:

- **(preferred) keep it on `type.@this` as the existing `Convert(item)` semantics, but give it an honest item-typed overload.** `Convert` is the established hook verb (not a Verb+Noun smell — it is the domain operation "this type converts X into itself"). A built value is an `item.@this`; an overload `Convert(item.@this value)` that does *only* the built-leaf→hook step (no raw-CLR `TryConvert` tail, no `null` arm the ctor already handles) is the cleanest target. The raw-CLR tail stays on the `object?` overload until Stage 5 thins it.
- Do **not** name it `Retype`, `ConvertValue`, `BuildValue`, or any Verb+Noun. (Flashing-sign rule.) `type.Convert(item)` reads as "type, convert this item" — owner-first, honest.

**This step is additive:** introduce/confirm the `Convert(item.@this)` overload (or prove the existing `Convert(object?)` is an acceptable Stage-3 target and defer the split to Stage 5). Existing `Build`/`Judge` callers are untouched. Add a unit test: `number.Convert(text "5")` → `number 5`; `number.Convert(text "abc")` → an Error (this is the validateResponse safety net).

**Preserve, from `Judge` (`type:538`), into 2a/2b — do not silently drop:**
- **same-name, missing-kind re-kind:** `Kind != null && minted.Kind == null` → `text.Kinded(Kind)` / `binary` kind stamp. A built value already of the declared type but lacking the kind must be re-kinded (this is a 2a sub-case: "same type, refine kind", not a no-op hold).
- **Facet match:** `value.Facet(Name) != null` → hold (the value carries the declared type as a facet — e.g. image has-a path). Mirrors `set`'s `keepAsIs`. This is a 2a hold, not a 2b convert.

Capture these as explicit 2a sub-rules so Stage 3 implements "hold" correctly rather than as a bare `type == declared`.

---

## Exit criteria

- [ ] Trace note / test proving Variable-target and `%ref%`-template survive on the from-raw `source` path (2.1).
- [ ] `type.Convert(item.@this)` (or the agreed Stage-3 target) exists and is tested: built-correct → re-typed; built-bad → Error.
- [ ] 2a sub-rules documented (same-type hold, same-type-missing-kind re-kind, facet-match hold) with the source lines they come from.
- [ ] `Build`, `Judge`, `Deserialize` still present and still called — nothing deleted.
- [ ] Global exit gates green.

## What must NOT happen

- No ctor change yet (Stage 3).
- No deletion yet (Stage 5).
- No Variable/`%ref%` special-case added to the ctor — it lives in `source`.
- No Verb+Noun name for the 2b op.
