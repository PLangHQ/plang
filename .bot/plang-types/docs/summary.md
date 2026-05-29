# docs — `plang-types` — summary

**Version:** v1 — PASS. Ready to merge.

## What this is

The `plang-types` branch lands the **typed value-system spine** for runtime2: higher-level PLang types (`number`, `image`, `code`) live as folders under `PLang/app/types/<name>/`, each owning its value, its parse-in factory, its build-time `kind` derivation, and its per-(type, format) serializer files. `int`/`decimal`/`double` become kinds of `number`; `jpg`/`png` kinds of `image`; `csharp`/`python` kinds of `code`. `code.load` extended to scan loaded DLLs for `[PlangType]` classes and `ITypeRenderer` implementations on top of `ICode`, with five sealed names (`identity`, `signature`, `signedoperation`, `callback`, `channel`) refusing shadowing to keep signing/transport bodies tamper-proof. Math handlers retyped through `number.* → Wrap`, gaining configurable Overflow/Precision policy via `app.config` plus per-action overrides. New `math.intdiv` action makes the truncating C# integer-division semantics explicit, freeing plain `math.divide` to do the non-surprise thing (`7 / 2 → 3.5`).

## What was done (docs v1)

XML doc quality on the new code is already strong — `number/this.cs`, `NumberPolicy.cs`, `Loader.cs`, `image/this.cs`, `code/this.cs`, `this.Arithmetic.cs`, `this.Unary.cs` all carry what/why-shaped summaries. The gaps were all elsewhere:

**User-facing docs (`docs/`):**
- `docs/modules/math.md` — added intdiv section, divide `7/2 → 3.5` example, Number Policy section (Overflow/Precision, app.config vs per-action, power exponent cap). Corrected `DivisionByZero` → `DivideByZero` to match production.
- `docs/modules/index.md` — math row now includes `intdiv`.
- `docs/modules/code.md` — `load` documents three contribution kinds (`ICode` + `[PlangType]` + `ITypeRenderer`), the sealed-name guard with `TypeLoadCollision`, and the honest limit on what runtime registration can't rewrite.

**Action teaching (`os/system/modules/`):**
- `os/system/modules/math/intdiv.description.md` — new (without it, the builder catalog hook is missing).
- `os/system/modules/math/divide.description.md` — extended to make the `7/2 → 3.5` choice explicit.

**Architecture (`Documentation/v0.2/`):**
- `Documentation/v0.2/good_to_know.md` — appended "Typed values" entry: folder contract, two-`Build`s distinction (action vs type), per-(type, format) renderer dispatch, composition-not-union, runtime DLL loading + sealed names, OBP Rule #9 pointer. Discoverability hook into the architect plan; not a parallel narrative.

**Canonical authority (`/CLAUDE.md`):**
- Applied architect's proposal — OBP Smell Checklist gains item 7 ("Courier reaches into `Data.Value`") with grep detection pattern, pointing at `object_pattern_formal.md` Rule #9.

## Code example

The user-facing pattern this branch makes possible — divide does the natural thing, intdiv is explicit, policy is one config away:

```plang
- divide 7 by 2, write to %a%                / 3.5
- integer divide 7 by 2, write to %b%        / 3

/ Strict overflow on this one sum
- add %x% and %y%, Overflow=Throw, write to %sum%
```

And what shipped at `code.load`:

```plang
/ Loads ICode providers AND [PlangType] classes AND ITypeRenderers from the DLL.
/ Attempting to shadow 'identity', 'signature', 'signedoperation', 'callback',
/ or 'channel' fails with TypeLoadCollision.
- load code 'plugins/my-types.dll'
```

## Notes / outstanding

- Security v2 F4 (math.round Decimals out-of-range → uncaught ArgumentOutOfRangeException) is open but non-blocking. I did NOT document a Round-clamps-Decimals behaviour in `docs/modules/math.md` because the clamp doesn't exist in code yet. When a future coder lands the fix, `math.md` Number Policy section is the natural home.
- The architect plan (`.bot/plang-types/architect/plan.md` + 7 stage files) remains the canonical design narrative for the typed-value system; `good_to_know.md` is the long-lived discoverability hook into it.
- No `character-proposals.md` on this branch — no character edits.
