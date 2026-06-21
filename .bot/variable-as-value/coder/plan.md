# variable-as-value — `%variable%` is a first-class `variable`, not text

**Branch:** `variable-as-value` (off `compare-redesign`).
**Origin:** debugging why the PLang builder couldn't build any goal — `llm.query Messages=%messages%` declined with *"%Messages% holds a list<llmmessage> — 'list' cannot be created from it"*. Root-caused with `--debug` (no guessing). This plan is the fix.

---

## The bug (what `--debug` proved)

`Plan` sets `%messages%` to a real `list`. The sub-goal `QueryAndValidatePlan` reads `llm.query Messages=%messages%` and gets **null** — declines.

It is **not** a conversion gap (4 isolation tests prove `list<dict>` / `JsonNode` / json-string → `list<llmmessage>` all convert), **not** scope, **not** `type=json`. It's the **`.pr`-load born path**.

### 5 Whys
1. `llm.query` fails → `Messages` resolves to `null.@this`.
2. Why null → the `.pr` param `Data` for `messages` is born null at **load** (its `Peek()` is `null.@this` before any runtime resolution).
3. Why born null → `Wire.ReadBody` runs the **declared type's reader** on the value: `list<llmmessage>`'s reader tries to *parse* the literal string `"%messages%"` into a list, can't, and returns the null citizen.
4. Why does it parse a `%ref%` → the born path doesn't distinguish a *reference* from *content*; it hands every value to the type reader (and the no-reader fallback does `json.Parse("%messages%")` → not JSON → null).
5. Root → **a `%ref%` is being coerced to its declared type at load.** A reference is not content; it must stay unparsed and resolve at `.Value()` — and the swallow (null instead of error) hid it.

---

## The design (settled with Ingi)

**A full-match `%x%` is a first-class `variable` value — separate from `text`.** The runtime then *knows* it holds a variable, instead of a `text` pretending to be one via a full-match hop.

### Shape: `variable<container>(name, kind)`
The `.pr` declares `type: {name:"list", kind:"llmmessage"}`. We know the **container** statically (`list`) but **not** the element as a C# `T` — the element rides as **kind** (a runtime string resolved through the registry). So:

```
new variable<list>("messages", kind: "llmmessage")
```

- `<list>` = the container (known statically; `dict`/scalar/none otherwise).
- `kind` = the element/subtype (`"llmmessage"`), registry-resolved at `.Value()`.
- It carries name + declared type → `Data.Type` reports `{name:list, kind:llmmessage}` and round-trips.

This **extends today's `Variable`** (`IRawNameResolvable`). Two doors, one concept — Ingi's "different meaning":
- **Name slot** (`set %x%`, loop item names): read `.Name`, never resolve (today's behavior).
- **Value slot** (`llm.query Messages=%messages%`): `.Value()` → resolve `%x%` via `Context.Variable.Get` → the real value → convert to container/kind → **error, not null** on mismatch (with a clean, example-bearing message — non-programmers read these).

### Born rule (in `Wire.ReadBody`, the `.pr`/wire read)
- **full-match `%x%`** → `variable<container>(name, kind)` from the declared `{name, kind}` — **never parsed** (no type reader, no `json.Parse`).
- **partial `"...%x%..."`** → `text` (genuine string interpolation, renders to a string).
- **everything else** → content for the type reader (a real list/dict/json literal borns as today).

### `.pr` type encoding fix
`type:"list<llmmessage>"` (a string) is the **wrong** structure — `Create` doesn't decompose `<>`, so it parses to one opaque name `"list<llmmessage>"`. Correct: **`type:{name:"list", kind:"llmmessage"}`**.
- Builder must **emit the structured form** for container+element params.
- Existing committed `.pr` carrying `"...<...>"` strings need a **hand-fix / rebuild sweep**.

### Kill the swallow (`json.Parse` guess)
The `Lift(json.Parse(value))` fallback is the raw-CLR-era *"guess the shape"* path. In born-typed, the value is already typed (or a ref to one) and converts via its own type. A `%ref%` must never reach `json.Parse`. Where a genuine value can't become its declared type → **error `Data`** (PLang surfaces errors as `Data`, never a thrown exception at the courier), with a `Type.Example`-bearing message.

---

## Parked (after the builder is green)
- Review the codeanalyzer findings Ingi flagged (incoming review) once the builder build is cleared.

## Scope / worklist
1. **`variable<container>`** — extend `app.variable.Variable` to carry container type + kind and resolve+convert+error at `.Value()`; keep `.Name` for name-slots.
2. **`Wire.ReadBody` born rule** — full-match `%x%` → `variable`; partial → `text`; content → type reader. No parse on refs.
3. **`.pr` type encoding** — container+element as `{name, kind}`; builder emission + hand-fix existing `.pr`.
4. **Error-not-null** — failed concrete-type coercion returns an error `Data` with a clean, example-bearing message (`type.Example`).
5. **Remove the `json.Parse` "guess" fallback** from the conversion/born path (scope carefully — other call sites may lean on it).
6. **Rebuild sweep** — regenerate `.pr` once the builder bootstraps; confirm the PLang suite goes green.

## Already done on this branch
- **Debug watch prints the PLang type** (`debug/this.cs`: `data.Type.Name`, dropped the useless `CLR:`/`HasCtx:` and the ambiguous `type=this`). This is a keepable fix — `type=this` (the bare `@this` name) actively misled the investigation; now it prints `list`/`null`/`dict`.
- **Conversion isolation tests** (`PLang.Tests/Types/App/ConversionGapTests/`) — 4 passing tests proving `list<dict>`/`JsonNode`/json-string/single-`dict` → `list<llmmessage>` conversion already works (so the gap is the born path, not conversion). Keep as regression pins.
- Diagnostic `.pr` hand-edit (`emitbuildevent.pr` channel→text) — a real stale-shape fix found en route.
- `Tests/Scratch/Hello.goal` — minimal repro goal.

## Open mechanics for the architect
- `variable<container>` vs today's `Variable`: confirm one evolved type (typed, kind-bearing) with name-slot/value-slot doors, not a sibling.
- `.Value()` order: resolve name → value → convert to container/kind → error on mismatch.
- Blast radius of removing the `json.Parse` fallback (call sites).
- Bootstrap order: hand-fix the builder's own `.pr` (the `{name,kind}` encoding + any `%ref%` params) enough for `plang build` to run, then a full sweep.
