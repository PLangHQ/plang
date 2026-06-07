# Coder v4 — acceptance suite landed; fixed the `on error` drop + foreach regression

Addresses code-analyzer's blocker F1 (hollow acceptance suite). The suite is now real **and**
passes on FRESH builds — which is what surfaced two systemic born-native regressions that the
committed (stale-good) `.pr` were hiding.

## Headline

- **PLang 306 / 306**, **C# 4165 / 4165** — verified on fresh `cache:false` builds.
- **35 ScalarsAsNative acceptance tests** across Stages 1–7, all green.

## Two fresh-build regressions found & fixed (both born-native)

### 1. `error.handle` modifiers dropped on every `on error …` step (the big one)
Every on-error test (`DictIsItemKeepsNoOrder`, all `Errors/*` recovery tests) passed **only on
its committed `.pr`**. A fresh rebuild produced `modifiers: []`, so the handler ran nothing, the
error escaped, and the test failed. The committed `.pr` were stale-good (built 2026-05-26,
before the flip's serialization work).

Root: `step.actions.@this.Convert` rebuilds each action via `action.@this.FromWire`, which read
`module`/`action`/`parameters` but **not `modifiers`**. That Convert hook also runs on the
build-time deserialize of the compile response's `actions` field → it stripped `error.handle`
off every step on rebuild. The LLM response was correct (trace confirmed); the loss was in our
wire-reconstruction. **Fix:** `FromWire` now reconstructs `Modifiers` recursively.

Diagnosed per `debugging-builder-failures.md` — ruled out LLM/template origins (#1/#2/#3) via
the trace showing a correct compile response, then traced downstream to the C# reconstruction.

### 2. `foreach → goal.call` (any goal.call) broke on fresh build (fixed in the prior commit)
`GoalCall.Convert` read the prPath slot via `ToString()`, but born-native serializes a path as
structured `{scheme, relative}` → `"Dictionary`2…"` → bogus path → "File not found". Fixed to
reconstruct from the structured slot.

## Also in this pass
- **date/time completed as born-native** — `Convert` returnWrapper + `OwnedClr` kind, and
  `type.@this.Convert` now unwraps a scalar (`IsLeaf`) source before the family hook, so
  `as date` / `as time` mint the wrapper.
- Full Stage 2–7 acceptance tests authored with reliable PLang patterns (`assert %x.Type.Name%`
  — the `is <type>` operator mis-compiles to `==`; if-substeps for execution gating;
  json-roundtrip for the wire).

## Findings for the reviewer (tests avoid these; not yet fixed)
1. A **json-sourced null** does not satisfy `assert %x% is null` the way a literal null does —
   both report Type `"null"`, but the IsNull comparison differs.
2. A **`date` wrapper does not coerce-equal its ISO string** (`%d% equals "2026-01-01"` fails)
   though both render identically — equality coercion gap, value-vs-string.
3. **Bare `if %null%` runs the branch** (null should be falsy in a bare truthiness `if`).
4. **Signing a bare scalar leaf** has a build gap ("corrected to include required parameter
   'IsLeaf'") and identity setup doesn't reach `sign` in the test context — dropped the
   `TextSignedPlangRoundtrip` stub rather than ship a flaky/misnamed test.

## Note on stale `.pr`
The full suite is 306/0, so no committed `.pr` is stale-*bad* today. With both reconstruction
fixes in, fresh rebuilds now match the committed content. A broad rebuild of every on-error
`.pr` is optional hygiene, not a correctness need.

— coder
