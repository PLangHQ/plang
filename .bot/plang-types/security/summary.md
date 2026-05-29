# security — plang-types

## Version

v1 (initial security review of the branch).

## What this is

The plang-types branch lands the value-system spine: a registry-fold +
per-(type, format) renderer dispatch + new `image`/`code`/`number` value
types + runtime DLL type-loading. ~177 files, ~12K insertions vs
`origin/runtime2`. Coder v3 + tester v3 PASS; codeanalyzer PASS earlier.
This is the security gate before auditor.

## What was done

Read the architect plan + coder reports for context, then audited the net-new
attack surface focused on:

1. **Runtime DLL loading** — `app/types/Loader.cs` (NEW) + `code/load.cs`
   delta. Confirmed Execute-gate trust boundary holds; flagged that
   `Loader.Register` lets a loaded DLL shadow built-in `[PlangType]` names
   like `identity` (F2, Low, accepted-risk under user-sovereign).
2. **Image intake** — `image/this.cs`, `image/this.Parse.cs`,
   `file/read.cs:36-40`. SixLabors.ImageSharp.Image.Identify is header-only
   (safe), but `byte[]` storage has no size cap — extension of the standing
   `ReadAllBytes` finding (F3, Low).
3. **Number arithmetic** — `number/this.Arithmetic.cs`. `DoPower` has no
   exponent cap; `for (long i = 0; i < expL; i++)` synchronous CPU spin
   (F1, Low).
4. **Wire integration** — `Wire.cs`, `this.Normalize.cs`, `TypedValueNode.cs`,
   `json/writer.cs`. Confirmed: (a) v1 builder-ergonomics depth-counter
   regression suite intact, (b) `json.Writer` fail-closed on unknown leaf
   types, (c) `RendererLookupMissed` thrown when a tagged value has no
   renderer.
5. **`code` value type** — value-only, never executes. Safe.
6. **`number.Parse`** — `InvariantCulture` throughout; no culture-confusion
   vector.

semgrep architectural baseline: 15 findings (unchanged from runtime2 baseline,
no new blockers).

### Verdict

**PASS.** Three Low findings, none merge-blocking:

- F1 — `math.power` exponent unbounded → CPU DoS
- F2 — Loader.Register can shadow built-in `[PlangType]` names (defense-in-depth)
- F3 — image byte intake inherits standing no-size-cap gap

### Files written

- `.bot/plang-types/security-report.json` — full attack-surface + finding details
- `.bot/plang-types/security/v1/plan.md` — review plan
- `.bot/plang-types/security/v1/result.md` — verdict + finding writeups
- `.bot/plang-types/security/v1/verdict.json` — `{status: pass}`

## Code example

The math.power pattern that motivated F1 — the loop is straight CPU spin
with no bound below `long.MaxValue`:

```csharp
// PLang/app/types/number/this.Arithmetic.cs:206-211
long b = a.AsInt64();
long r = 1;
for (long i = 0; i < expL; i++) r = checked(r * b);
return (a.Kind == NumberKind.Int && r >= int.MinValue && r <= int.MaxValue)
    ? From((int)r) : From(r);
```

The fix posture is a one-line cap at the top of `DoPower`: refuse `|expL| >
Config.MaxPowerExponent` (default 64), surface `PowerExponentTooLarge`
through the existing `Wrap` envelope.
