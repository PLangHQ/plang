# security v1 — plang-types — VERDICT: PASS

Three Low-severity findings. No critical/high. Full details in
`/workspace/plang/.bot/plang-types/security-report.json`.

## What was reviewed

Net-new attack surface from the plang-types value-system spine (~177 files
vs `runtime2`, primary focus on production C# additions):

- `app/types/Loader.cs` — runtime DLL scan/register for `[PlangType]` + `ITypeRenderer`
- `app/types/renderers/this.cs` — per-(type, format) dispatch table
- `app/types/{image, code, number}/**` — new value types
- `app/types/Registry.cs` + primitives + kinds — name resolution seam
- `app/data/Wire.cs`, `this.Normalize.cs`, `TypedValueNode.cs` — wire integration of typed values
- `app/modules/code/load.cs`, `app/modules/file/read.cs` — call sites that funnel external data in
- `app/modules/math/**`, `app/modules/condition/Operator.cs` — number-using callers

semgrep architectural baseline: **15 findings (unchanged from baseline)** — all
known serializer-hygiene hits, no new blockers on this branch.

## Findings (Low × 3)

### F1 — `math.power` exponent is unbounded → CPU DoS

`DoPower` at `PLang/app/types/number/this.Arithmetic.cs:208` (and three sibling
branches at 193–195, 215–217, 223–226) loops `for (long i = 0; i < expL; i++)`
with no cap below `long.MaxValue`. A `%user_exp%` of 10⁹ spins the actor's
core for seconds; 10¹⁵ effectively hangs the worker. `OverflowMode.Throw`
doesn't help — the loop completes silently on small `b`. Reachable from any
goal that lands untrusted input into a `math.power Exp=%var%` slot.

**Fix:** cap `|expL|` at e.g. `Config.MaxPowerExponent` (default 64) at the
top of `DoPower`; past it, surface a typed `PowerExponentTooLarge` `Data.Fail`
through the existing `Wrap` envelope. One-line guard.

### F2 — Loader.Register lets a third-party DLL shadow built-in `[PlangType]` names

`code.load`'s Execute gate authorises the DLL load. Past it, `Loader.Register`
(at `PLang/app/types/Loader.cs:66, 89`) registers any `[PlangType("name")]`
into `_runtimeNameToType` and any `ITypeRenderer` into `_runtime`. Both win
over generator-emitted entries at `ResolveType` / `Renderers.Of`. So a DLL
declaring `[PlangType("identity")]` + a matching renderer replaces how
`identity` resolves and how its **body** renders on the wire.

The signing pipeline is **not** broken — `Wire.Write` still calls
`EnsureSigned` with the actor's real key, so the outer signature is
authentic. The catch: the body it signs is renderer-controlled. A downstream
verifier sees a valid signature over a body the loaded DLL composed.

Under PLang's user-sovereign threat model (`code.load` past Execute = user
trust), this is **accepted-risk** rather than a bug — but the trust transfer
is non-obvious: "load a DLL that adds a number kind" silently confers the
right to rewrite Identity's wire body. Worth memorialising.

**Defense-in-depth fix:** reserve a small allowlist of "sealed" built-in
names — `identity`, `signature`, `signedoperation`, `callback`, `channel` —
that `Loader.Register` refuses to shadow (return a `TypeLoadCollision` error
key). Primitives like `int`/`string`/`path` stay overridable because their
body is constrained by the type itself.

### F3 — `image.@this` byte intake has no size cap (standing finding extension)

`file.read` constructs `new image.@this(bytes, mime, Path.Value)` for any
`image/*` MIME read (`PLang/app/modules/file/read.cs:38`) with no max-bytes
guard. `image.@this.ResolveAsync` calls `p.ReadBytes()` on any path scheme
including `http://` with no cap. `SixLabors.ImageSharp.Image.Identify(Bytes)`
in the lazy `Width`/`Height` getters is header-only (safe), but the raw
`byte[]` itself is the OOM vector.

Today this is an extension of the standing OpenAI-provider `ReadAllBytes`
finding — local DoS scope, user-gated AuthGate(Read). Becomes channel-shaped
intake the moment any handler exposes `Data<image>` with a string parameter
(no such handler ships today; the `ResolveAsync` factory is a latent surface).

**Fix:** consolidate at the path-verb layer — `ReadBytes(maxBytes?)` —
applied at `file.read` image construction and at any future `Data<image>`
binding site. Past the cap, `ImageTooLarge` `Data.Fail`. Cross-references
the pre-existing standing finding.

## What I checked and ruled out

- **`Wire.LiftDataIfShaped` rehydration restart vector** — the v1 builder-ergonomics
  fix is intact (`_readDepth` AsyncLocal counter capped at `MaxReadDepth = 64`
  at `PLang/app/data/Wire.cs:113-114`). The `ReadPropertyPrimitive` recursion
  inside `properties` is bounded by the outer STJ reader's depth budget.
  No regression.
- **`json.Writer` fail-closed default** — unknown value types throw
  `NormalizeUnexpectedLeafType` rather than falling back to STJ reflection
  (which would bypass `[Out]`/`[Sensitive]`/`[Masked]` filters). Discipline
  preserved.
- **`number.@this.Parse` parser DoS** — `InvariantCulture` throughout, no
  culture-confusion vector; overflow / format errors return null cleanly
  (Parse) or throw `FormatException` for `Resolve` (handler-side typed-error
  surface).
- **`code.@this`** — value-only type; stores `Source` + detected `Language`,
  never executes. `DetectLanguage` is a pure substring heuristic.
- **`DoOp` overflow recovery** — confirmed by tester v3: Throw vs Promote
  distinction is load-bearing (`Long.MaxValue + Long.MaxValue` mutation test
  passed).
- **Renderer ctor invocation in `Loader.Register`** — wrapped in catch except
  OOM/SOF; parameterless-only rule mirrors `code.load`'s existing template.
  No new ACE surface beyond the already-accepted `code.load` boundary.
- **`Wire.ReadBody` `properties` field** — recursive `ReadPropertyPrimitive`
  inside the outer STJ reader; STJ's MaxDepth bounds it. No restart vector.

## Verdict

**PASS.** Branch is safe to merge. The three Low findings should be tracked
but none gate the merge. The runtime type-loading spine is well-shaped:
Execute-gated, fail-closed renderer dispatch, runtime-wins precedence is
by-design and documented in the architect's plan.

## Next bot

```
VERDICT: PASS
Next: run.ps1 auditor plang-types "Review the code on branch plang-types" -b plang-types
```
