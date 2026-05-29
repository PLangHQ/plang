# tester v2 — data-serialize-cleanup

This version follows the codeanalyzer v2 PASS on the branch. The coder shipped
Stages 1–5 + F1–F11 fixes; no `.bot/data-serialize-cleanup/coder/` folder or
`baseline-tests.md` was written (see Process note below), so version skipped from
v1 → v2 to match the codeanalyzer.

## Approach

1. Clean rebuild (stale-binary trap).
2. C# suite: `dotnet run --project PLang.Tests`.
3. PLang suite: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.
4. Read every new test file for honest assertions vs false-greens.
5. Read every new `.test.goal` and its `.pr` for builder false-greens.
6. Mutation-test the load-bearing claims (Stage 2 canonicalization).

## Result

- **C# 3229/3229 pass**, **PLang 228/228 pass**, build clean (0 errors).
- Mutation: replacing `serializer.OutboundOptions` with `JsonSerializerOptions.Default`
  in `crypto/code/Default.cs:51` correctly fails three tests
  (`CryptoHash_UsesTransportForOutboundOptions_NotDefaultStj`,
  `Cut4_TamperingPropertyValue_FailsOuterSignatureVerify`,
  `OuterSignature_AfterPropertiesValueTamper_FailsVerify`). Stage 2's
  canonicalization fix is honestly covered — initial concern (no test tampers
  inner.signature only) was invalid.
- Two minor findings (stale comment + weak `%archived!Type%` assertion).
- One process note (no coder/ folder).

Verdict: **PASS**.
