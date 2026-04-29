# Security v1 — runtime2-builder-bootstrap

## What this is

First security pass on the builder-bootstrap branch (~200 C# files changed since
runtime2). Threat-model framing: the user is sovereign, signatures on Data and
.pr files are the trust boundary; we audit confidentiality leaks and
deserialization/type-confusion paths against that boundary.

## What was done

Triaged the branch by trust-boundary relevance, deep-read the new and most-
changed files in `PLang/App/`, and produced a structured report at
`.bot/runtime2-builder-bootstrap/security-report.json`.

**Verdict: pass.** No critical/high open findings. 2 medium info-disclosure
findings and 4 low tripwires.

## Key findings

1. **F1 (medium) — ParamSnapshot bypasses [Sensitive].** New
   `LazyParamsGenerator.__SnapshotParams` captures `PrValue` and `FinalValue`
   for every property (including `[Sensitive]` ones), the snapshot lives on
   `Error.Params`, and `Error.cs FormatVerboseValue` prints both fields with no
   mask. Cleartext secrets from `[Sensitive]` handler params leak into stderr,
   debug stream, and any log capture on every error.

2. **F2 (medium) — Standing finding confirmed.** `Variables.Snapshot()` and
   `Variables.GetAll()` still return raw dict values without sensitivity
   filtering. `SensitivePropertyFilter.Mask` only acts on typed-property
   metadata, so a top-level `%apiKey% = "sk-..."` leaks through
   `AssertionError.Variables` into `results.json` and through verbose error
   dumps.

3. **F3-F6 (low) tripwires:** JSON deep-clone silent alias-fallback in
   `Variables.Set`, unbounded recursion in `FluidProvider.UnwrapFluid`,
   unsanitized `traceId` in `Debug.ResolveLlmFilePath`, and the intentional
   `BuildingGuard` removal letting signed runtime goals call
   `builder.goalsSave` (flagged for architect confirmation, not a bug).

## Code example — F1 mechanics

```csharp
// LazyParamsGenerator.cs:701-703 — generated for every handler property
PrValue   = __pr?.Value,                                  // no [Sensitive] check
PrType    = __pr?.Type?.Value,
FinalValue = __ApiKey_set ? (object?)__ApiKey_backing : null,  // cleartext secret

// Error.cs:215, 220 — every error renders these
var pr    = FormatVerboseValue(p.PrValue);                // no mask
var final = FormatVerboseValue(p.FinalValue);             // no mask
```

Suggested fix: in the generator, when `prop` carries `[Sensitive]`, emit
`PrValue = "******"` and `FinalValue = WasAccessed ? "******" : null`.
Alternatively route `Error.cs` rendering of `ParamSnapshot` through
`Json.FormatForDiagnostic` (which uses the masking serializer).

## Recommendation

**Suggest auditor next** — verdict is pass. The two medium findings are
worth a coder follow-up but not blocking; the audit can land. Auditor should
re-verify the standing `Snapshot` finding hasn't regressed further.
