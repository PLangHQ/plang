# v1 review summary — what landed since security v1 PASS

The branch advanced through `d963fcf55..b32fd0dfe` with the following arc:

- **coder v4 (8c99912a0)** — addressed security v1 F1 (math.power exponent
  cap, added `MaxPowerExponent = 64` + `PowerExponentTooLargeException` +
  Wrap catch branch) and F2 (`Loader.SealedNames` allowlist refusing
  identity/signature/signedoperation/callback/channel at PlangType-explicit
  and inferred-name gates). Deferred F3 (image byte cap) as architectural
  cleanup belonging on a different branch.
- **codeanalyzer v2 (118276e6d)** — review notes, no blockers. Reshaped
  the F1 cap into per-branch `EnsureExponentInRange` calls so Math.Pow
  branches legitimately skip; reshaped sqrt's negative-input key to
  `ArithmeticError` (one canonical key across direct and handler call).
- **coder (a58dcfeee)** — applied codeanalyzer-v2 minors.
- **mathhelper-deletion merge (1bb5224b6)** — retyped abs/floor/ceiling/sqrt/
  round/min/max handlers through `number.*` instead of the bespoke
  `MathHelper.ToDouble`/`PreserveType`. Adds new attack surface for me to
  audit: parameters that escape `Wrap`'s catch envelope.
- **tester v4 (ba5f3d21b)** — FAILED. Coder had tested the sealed gate at
  one of three sites (explicit `[PlangType]`). The inferred-name branch
  (`Loader.cs:101`) and the ITypeRenderer-registration pass (`:122`) were
  untested; mutation neutralised both with `if (false && …)` and all nine
  RuntimeTypeLoadingTests stayed green. Also flagged a weak sqrt handler
  assertion and stale Sqrt docstring.
- **coder v5 (1cdb0a840)** — added two fixtures
  (`SignatureRendererShadow.dll`, `CallbackInferredShadow.dll`) and the
  corresponding gate-site tests; strengthened sqrt assertion; refreshed the
  Sqrt docstring.
- **tester v5 (b32fd0dfe)** — PASS. Mutation-confirmed all three sealed
  gate sites independently fail when their check is neutralised; the F2
  handler-key strengthening is genuinely load-bearing (forcing
  `DoSqrt → DivideByZero` flips the test red).

What this means for security: my v1 F1 and F2 are mechanically closed,
with mutation testing at the gate sites I cared about. The
mathhelper-deletion merge introduced one new finding to surface (F4 — see
result.md).
