# docs v1 — result

## Verdict

**PASS.** All identified gaps filled. Branch is ready to merge.

## What changed

### User-facing (`docs/`)

- **`docs/modules/math.md`**
  - Header note: `int`/`long`/`decimal`/`double` are *kinds* of one PLang type `number`; behaviour configurable via Lenient/Strict policy.
  - **`divide`** — example for `7 / 2 → 3.5`; called out that divide always leaves the integer track; pointer to `intdiv`. Error key corrected to `DivideByZero` (was `DivisionByZero` — production code uses `DivideByZero`, confirmed in `this.Arithmetic.cs`).
  - **`intdiv`** — new action section with example, semantics, pairing with `modulo`.
  - **Number Policy** section — Overflow (Promote/Throw), Precision (Double/Decimal), app-config vs per-action setting, power exponent cap.
- **`docs/modules/index.md`** — math row's action list updated to include `intdiv`.
- **`docs/modules/code.md`** — `load` now documents that the assembly is scanned for `ICode` + `[PlangType]` classes + `ITypeRenderer` implementations. Sealed-name guard (`identity`, `signature`, `signedoperation`, `callback`, `channel`) called out with `TypeLoadCollision`. Honest limit on what runtime registration can't rewrite (compiled slots / shipped `.pr` stamps).

### Action teaching (`os/system/modules/`)

- **`os/system/modules/math/intdiv.description.md`** — new file; without it the builder doesn't surface `math.intdiv` cleanly.
- **`os/system/modules/math/divide.description.md`** — extended to make the `7/2 → 3.5` choice explicit and point to `intdiv` for truncating semantics.

### Architecture (`Documentation/v0.2/`)

- **`Documentation/v0.2/good_to_know.md`** — appended "Typed values — `app/types/<name>/`, per-(type, format) renderers, `type` + `kind` as separate fields". Covers the type folder contract, the build-time `Build(value) → kind` distinction from action `IClass.Build()`, the per-(type, format) serializer file shape, composition-not-union for multi-faceted values, runtime DLL loading with sealed names, and OBP Rule #9 ("couriers never read `.Value`"). Pointers to the architect plan for the full design.

### CLAUDE.md (canonical authority)

- **`/CLAUDE.md`** — applied architect's proposal: item 7 added to the OBP Shape Smells checklist ("Courier reaches into `Data.Value`"), pointing to `Documentation/v0.2/object_pattern_formal.md` Rule #9 for the full rule.

## What I did NOT write

- **New PLang `.goal` examples** for `math.intdiv` / `image` / `code` — tester's job, not docs'. Existing test coverage on the branch is strong (3636/3636 C#, 248/248 plang); the `intdiv` PLang surface is exercised through the builder examples in `PLang/app/modules/math/intdiv.cs:18-24` and through the tester's coverage. No new gap.
- **Net-new `Documentation/v0.2/typed-values.md` deep dive.** The architect plan (`.bot/plang-types/architect/plan.md` + 7 stage files) is the canonical design narrative; `object_pattern_formal.md` Rule #9 is the canonical OBP rule; the new `good_to_know.md` entry is the discoverability hook between them. Duplicating any of that into a separate file would rot the moment the plan moves.
- **XML doc fixes** — the new code (`number/this.cs`, `image/this.cs`, `code/this.cs`, `NumberPolicy.cs`, `Loader.cs`, `this.Arithmetic.cs`, `this.Unary.cs`) already carries what/why-shaped summaries on every public surface. Nothing to add that wouldn't be noise.

## Outstanding security finding

Security v2 flagged **F4 (math.round Decimals out-of-range)** as a new Low — `math.round` with `Decimals` outside `[0, 28]` (decimal) or `[0, 15]` (double) throws `ArgumentOutOfRangeException`, which `Wrap` doesn't catch. Not blocking per security's verdict, but I did NOT document a Round-clamps-Decimals behaviour in `docs/modules/math.md` because the clamp doesn't exist in code yet (`this.Unary.cs:84-90`). If a future coder lands the clamp, the math.md Number Policy section is the natural home for the "Out-of-range Decimals → `ArithmeticError`" note.

## Notes

- The branch is post-merge: `mathhelper-deletion` already folded in (commit `1bb5224b6`). All math handlers route through `number.* → Wrap`.
- `Documentation/v0.2/todos.md` already captures the larger `PLNG003` / `ITypeRenderer.Read` / image byte-cap deferrals — no docs work needed there.
