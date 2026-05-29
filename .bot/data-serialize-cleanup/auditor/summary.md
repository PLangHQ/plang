# auditor — data-serialize-cleanup

**Version:** v1

## What this is

Second-opinion review of the data-serialize-cleanup branch — the architectural pass that untangles PLang's serialization boundary (Stages 1–5: ISerializer tightened to Data, plang serializers merged, signing folded into the wire converter, canonicalization fixed to bind inner signatures, Compress flattened, Properties given a nested wire scope, vocabulary swept).

Three reviewers already PASSED:
- **codeanalyzer v2** — coder closed all 11 v1 findings.
- **tester v2** — 3229/3229 C# + 228/228 PLang; mutation-verified Stage 2 canonicalization.
- **security v2** — F1 retracted HIGH→Info after mutation test; depth bomb already gated by STJ's per-reader MaxDepth.

My job: look at what they missed by being inside their slice.

## What was done

Cross-file walks:
1. **Canonicalization chain** — `WireJsonConverter` ↔ `crypto/Default.cs` ↔ `plang.@this.OutboundOptions` ↔ `ContextLessFallback`. Confirmed hash bytes ≡ wire bytes minus outer Signature, inner Datas bound. Static-initialiser order acyclic.
2. **`!` operator end-to-end** — `Variable.Resolve` malformed-shape detection ↔ `variable/set.cs` `IsMalformed` gate ↔ `Properties.EnsureSupportedValue` ArgumentException catch. Composes.
3. **Properties triangle** — insertion gate / wire write / wire read / navigation. `LiftDataIfShaped` heuristic confined to `value` slot; `properties` immune. Asymmetric round-trip risk on smuggled `List<Data>` is documented.
4. **Depth-bomb trace** — re-walked the path through `ParseValue` and confirmed it inherits the source reader's MaxDepth; recursion ladder is `MaxDepth × constant`-bounded, not payload-depth-bounded. Security v2's retraction is correct.

Files written:
- `auditor/v1/plan.md`
- `auditor/v1/result.md`
- `auditor/v1/verdict.json`
- `auditor-report.json` (at branch root)

## Verdict

**PASS.** Branch is merge-ready.

## Findings (both minor)

1. **`%archived!Type%` test assertion** — passes via `this.Navigation.cs:318` reflection fallback, not the Properties scope. Endorses tester F1.
2. **`crypto/hash.cs:18` async-without-await** — `public async Task<…> Run() => Crypto.Hash(this);` wraps a sync call. CS1998 cosmetic, appears suppressed at project level.

Process note: no `coder/` folder exists for this branch (no `summary.md`, no `v<N>/plan.md`, no coder session in `report.json`) — tester already flagged.

## Code example — the canonicalization seam I walked

```csharp
// crypto/Default.cs:48 — pick the registered serializer or the canonical fallback
var registered = action.Context?.Actor?.Channels.Serializers.GetByType("application/plang");
if (registered != null && registered is not plang.@this)
    return FromError(new ActionError("…", "SerializerMismatch", 500));
var serializer = (registered as plang.@this) ?? _fallbackPlang;

// Tell WireJsonConverter to suppress THIS one Data's Signature and EnsureSigned
using (WireJsonConverter.MarkOuterForHash(data))
{
    // Same OutboundOptions the wire egress uses → hash bytes ≡ wire bytes
    bytes = JsonSerializer.SerializeToUtf8Bytes(data, serializer.OutboundOptions);
}
```

The discipline at this site is: same options bag both sides, outer Signature suppressed by ref-counted marker, inner Datas walk through full sign-if-missing → outer signature transitively binds them. That's what the tester's mutation test (`OutboundOptions → JsonSerializerOptions.Default` fails 3 canonicalization tests) actually proves.
