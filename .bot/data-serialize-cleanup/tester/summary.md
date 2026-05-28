# tester — data-serialize-cleanup

**Version:** v2 (matches codeanalyzer v2 — coder shipped Stages 1–5 + F1–F11 in
one body of work; no `coder/` folder was written, so v1 was never scoped).

## What this is

Validates test quality on `data-serialize-cleanup` — the architectural pass that
untangles PLang's serialization boundary: ISerializer tightened to Data, plang
serializers merged, signing moved into the wire converter, canonicalization
fixed to bind inner signatures, Compress flattened, Properties given a nested
wire scope.

The architect produced a five-stage plan; the test-designer wrote ~90 failing
stubs (C# + 7 PLang `.test.goal` files); the coder filled them all in and
addressed every codeanalyzer v1 finding (F1–F11). My job: confirm those tests
are honest.

## What was done

1. Clean rebuild (PLang/PlangConsole/PLang.Tests/PLang.Generators bin/obj
   purged) to dodge the stale-binary trap.
2. Ran full C# suite: **3229/3229 pass**.
3. Ran full PLang suite: **228/228 pass**.
4. Read every new Serialization test file (`PLang.Tests/App/Serialization/*.cs`
   + `IntegrationCuts/*.cs`, 18 files, ~2200 lines).
5. Read every new `.test.goal` and verified `.pr` step-text → module.action
   semantically matches (no builder false-greens).
6. **Mutation-verified Stage 2's canonicalization fix**: replaced
   `serializer.OutboundOptions` with `JsonSerializerOptions.Default` at
   `PLang/app/modules/crypto/code/Default.cs:51`. Three tests correctly failed:
   - `CryptoHash_UsesTransportForOutboundOptions_NotDefaultStj`
   - `Cut4_TamperingPropertyValue_FailsOuterSignatureVerify`
   - `OuterSignature_AfterPropertiesValueTamper_FailsVerify`

   Reverted before commit; `git status` clean.

## Findings

- **F1 (minor, weak-assertion):** `Tests/Serialization/CompressRoundTrip.test.goal`
  uses `%archived!Type%` to assert `'archived'`. The `!`-operator falls through
  to Data infrastructure reflection (`this.Navigation.cs:318`) when no
  Property matches, so this passes via `Data.Type` reflection, not via the
  Properties scope the test reads like it's checking. Compress sets
  `Data.Type` (correctly) — not `Properties["Type"]`. Use
  `%archived.Type.Value%` or split into two disjoint cases.

- **F2 (minor, weak-assertion):**
  `WireConverterSigningTests.WireConverter_DoesNotWalkProperties_AsDataNodes`
  and `Cut1_PlainRoundTripTests.Cut1_WireJson_HasFourTopLevelFields...`
  both assert `properties` is absent on the wire — comment says
  `pre-Stage-4`, but Stage 4 has landed. They pass for the right structural
  reason (empty Properties is omitted) but the stated contract is stale.
  Rename / update comments, or delete as duplicates of `PropertiesWireShape`
  coverage.

- **Process note (not a coder finding):** `.bot/data-serialize-cleanup/coder/`
  doesn't exist — no `summary.md`, no `v1/plan.md`, no `baseline-tests.md`, no
  coder session in `report.json`. Per the coder character file these are
  mandatory. Tester's instructions also call out "flag as process violation"
  for missing `baseline-tests.md`. Flagged; tests/code pass so not blocking.

## Verdict

**PASS.** Branch is merge-ready.

## Code example

The mutation test that proved Stage 2 is honest:

```csharp
// Replace this:
bytes = JsonSerializer.SerializeToUtf8Bytes(data, serializer.OutboundOptions);
// With this (mutation):
bytes = JsonSerializer.SerializeToUtf8Bytes(data, JsonSerializerOptions.Default);
// → 3 tests fail. Revert. Now back to 3229/3229.
```

The Stage 2 work is properly load-bearing: hash bytes ≡ wire bytes (minus the
outermost Signature, suppressed via `WireJsonConverter.MarkOuterForHash`), so
tampering anything that touches the wire — including inner signatures inside
nested Datas — invalidates the outer hash.
