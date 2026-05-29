# security v1 — plang-types

## Scope

Diff vs `origin/runtime2` (~177 files; ~12K insertions). Focus on net-new
attack surface introduced by the plang-types value-system spine:

- `app/types/Loader.cs` (NEW) — runtime DLL `[PlangType]` + `ITypeRenderer` scan/register
- `app/types/renderers/this.cs` (NEW) — per-(type, format) dispatch
- `app/types/image/**` (NEW) — image value (bytes + MIME, ImageSharp dimension probe)
- `app/types/code/**` (NEW) — code value (source + detected language)
- `app/types/number/**` (NEW) — number value, arithmetic, policy, parse
- `app/types/path/serializer/Default.cs` (NEW) — wire renderer for path
- `app/types/primitives/this.cs`, `app/types/kinds/this.cs`,
  `app/types/Registry.cs` updates — name/kind/registry seam
- `app/data/Wire.cs` (Sign-if-missing semantics unchanged; depth guard intact),
  `this.Normalize.cs` (TypedValueNode tagging), `TypedValueNode.cs` (NEW)
- `app/modules/code/load.cs` — calls `Loader.Register` post Execute gate
- `app/modules/file/read.cs` — image stamping on `image/*` MIME
- `app/modules/math/**` — math handlers route through `number.Arithmetic`
- `app/modules/condition/Operator.cs` — number recognized in widening

## Method

1. semgrep baseline (architectural invariants) — confirm no new blocking findings beyond the
   known 15-entry serializer-hygiene baseline.
2. Read `Loader.cs` and `code.load` for what executes/registers when a third-party DLL is loaded
   (trust boundary is Execute on the DLL path; everything after is user-sovereign).
3. Trace `image.@this.ResolveAsync` for external-byte intake; check for size limits.
4. Trace `number.@this.Parse` for parser-time DoS (locale / culture / overflow handling).
5. Trace `code.@this.Resolve` — confirm it stores source, never executes.
6. Trace `Wire.Read` + `LiftDataIfShaped` heuristic — confirm the v1 stack-overflow fix
   (AsyncLocal `_readDepth` capped at 64) is unchanged and the lift heuristic doesn't widen
   the inbound trust surface.
7. Trace `Normalize` `TypedValueNode` tagging + `json.Writer` dispatch — confirm fail-closed
   on unrecognised types.
8. Sanity pass on `number.Arithmetic` — overflow/divide-by-zero handling, exponent bounds.

## Threat model anchors

- `code.load` already has Execute-gate semantics — the user authorising load implies trust in
  the binary. Anything `Loader.Register` does post-load (PlangType shadowing, renderer
  registration overwriting Identity rendering, parameterless ctor invocation) inherits that
  trust. Worth memorialising what shadowing actually buys an attacker, but **not** flagging
  as bug.
- `image` / `code` / `number` are value-types — never executed. Concern is intake-side:
  HTTP-fetched bytes, large literals, parser-time CPU.
- `Wire` is the trust-crossing point; the v1 finding's regression-suite remains in place
  (the AsyncLocal depth counter at line 113-114). Confirm no new restart-vector was added.
