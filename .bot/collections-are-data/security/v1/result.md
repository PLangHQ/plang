# Security v1 — collections-are-data — PASS

**Next bot: auditor**

## Verdict

PASS. No critical/high open findings. Branch is sound at the security boundary.

**Merge gate carries from codeanalyzer v4:** do NOT merge `collections-are-data` to `main` ahead of sibling branch `signature-as-schema-wrapper`. F2 below is the reason — fail-closed regression, not exploitable, but ships a verify-side UX break for users that put a signed Data into a list or hand it to a sub-goal.

## What I looked at (49 commits since type-kind-strict)

- **Wire `@schema:"data"` marker** (`Wire.cs:422`, `data/this.cs:46`, `data/this.cs:1344`) — Data self-identifies on the wire. Strict marker recognizer (object + `@schema:"data"` key) replaces the old "name+value shape-sniff". UnwrapJsonElement lift through the marker is depth-capped at MaxJsonDepth=128. Strict-kind validation lives in `variable.set`, runs post-lift regardless of how the Data was built — the lift does not bypass it. **Clean.**
- **list/dict native types** (`list/this.cs`, `dict/this.cs`, `where.cs`) — chunk/row list model with CopyStructure on the `add`/`set` merge boundary; dict is an ordered key→Data store with OrdinalIgnoreCase last-wins on dup keys. F1 (list aliasing) was fixed by coder v6 and deletion-tested by codeanalyzer v4. **Clean** at the security boundary; two low informational findings below.
- **`where` action** (`list/where.cs:1`) — `Field` is a name string, `Operator` is a typed Operator, `Value` is a parameter. No expression injection; navigation is GetChild(field) into the subject. **Clean.**
- **F2 signing regression** — verify-through-list/goal-call is broken (Data wrapping Data rehash mismatch). Tester v7 confirmed honest Skipped (271 pass + 2 skipped, deterministic). Reframed below.
- **semgrep** — 17 findings, all `serializer-default-options`. 15 are baseline; 2 are net-new on this branch (`text/this.Convert.cs:32`, `path/http/this.cs:448`). text.Convert is genuinely user-reachable for a [Sensitive] leak (F2 below).

## Findings

### F1 — `verify` returns false on a signed Data round-tripped through a list or goal call. **Medium · accepted-risk · deferred to signature-as-schema-wrapper**

`Wire.cs` lifts a serialized signed-Data through the array arm / parameter pass as `Data{value: Data{value: original}}`. `Ed25519.VerifyAsync` step 7 rehashes the outer and gets a different digest than the originally-signed inner; `DataHashMismatch` is returned.

**Severity calibration.** This is **fail-closed**. Verify rejects the legitimately-signed value — it does *not* accept a forgery. No attacker capability is granted. The real risk shape is downstream: users routing around the false-negative by skipping verify, stripping signatures, or hand-rolling their own check, which weakens posture for the next channel-ingress branch.

**Confirmation.** Tester v7's actions log records the un-gut-and-rerun reproduction (`SignedDataSurvivesInList.test.goal` un-gutted on the current branch binary → `[Fail]` 200ms; reverted clean). Two disabled .test.goal documents the regression precisely with the un-gut path written into the comments. C# verify-against-raw probe (`SignedDataSurvivesVariableSetListTests.cs`) still covers the primitive — the gap is the wire-aware verify path, not the crypto.

**Merge gate.** Documented in codeanalyzer v4; intact. Carry it.

**Files:** `PLang/app/data/Wire.cs`, `PLang/app/data/this.cs`, `PLang/app/module/signing/code/Ed25519.cs`, `Tests/LazyDeserialize/Signed*.test.goal`.

### F2 — `text.Convert` serializes native dict/list with no options → loses `[Sensitive]` redactor. **Low · open**

`PLang/app/type/text/this.Convert.cs:32` calls `JsonSerializer.Serialize(value)` with **no** options object on the native dict/list/IEnumerable arm. Result: any value graph reaching `as text` through a dict/list serializes without the `[Sensitive]` redactor, without `PathJsonConverter`, without `IgnoreCycles`.

**Reach today.** Narrow. The only first-class `[Sensitive]` surface is `Identity.PrivateKey`. A user (or LLM-generated step) does `set %list% = [%identity%]; set %t% = %list% as text` → `%t%` contains the private key in plain JSON. Standing precedent (`memory/feedback_secrets_in_test_artefacts` — Variables snapshot in test/report.cs, same shape, same call). The serializer-default-options semgrep rule flagged this; baseline was 15, now 17.

**Fix.** Pass `Conversion.ContextualReadOptions(context)` (or `Format.Options`) so the [Sensitive] filter and IgnoreCycles apply. Mirrors the data-normalize fix.

**Files:** `PLang/app/type/text/this.Convert.cs`.

### F3 — `list.@this.CopyStructure()` has no explicit depth guard. **Low · open**

`PLang/app/type/list/this.cs:155` recurses through every nested-list row with no `int depth` parameter and no MaxJsonDepth check.

**Reach today.** Bounded by `MaxJsonDepth=128` on the only wire-inbound path that constructs nested list shapes (`data.UnwrapJsonElement`). In-process construction (`set %x% = ...` building 10k+ deep) is user-sovereign — not an attacker capability. Latent: any new lift path that loses the depth cap (a new channel serializer, a new Convert override) re-exposes the StackOverflowException.

**Fix.** Add `int depth = 0`, throw past `MaxJsonDepth`. Mirrors `UnwrapJsonElement` and `Identity.Hash`'s recursion-pattern.

**Files:** `PLang/app/type/list/this.cs`.

## Standing finding cross-reference

- `pattern_strict_kind_reflection_probe` — `variable.set` strict-kind validator from type-kind-strict. Confirmed the `@schema` lift does NOT bypass this gate (the validator runs at `set` time on the lifted Data, not at lift time).
- `pattern_authgate_canonicalization` — closed on purge-systemio-from-actions/v2. Nothing on this branch reopens it.
- The `Variables.Snapshot()` [Sensitive] leak (medium, standing-open) — same shape as F2 here; both want the same redactor-options fix.

## What I checked and decided was clean

- `where` action — no expression injection; `Field` is a name, `Operator` is typed.
- `dict.@this` OrdinalIgnoreCase last-wins — documented insertion-order contract, no key-shadowing attack surface.
- `CopyStructure` "leaves shared by reference" — leaves are weight-1 scalar/dict Data; `set %x% = ...` rebinds rather than mutates, so the by-reference share doesn't create write-through aliasing (codeanalyzer v4 verified this with the permanent regression test `ListTests.Add_List_DoesNotAliasSourceVariable`).
- `@schema` marker — strict (object + key + string equals "data"), no shape-sniffing, replaces the old ambiguous recognizer. Net security improvement.
- F2 disabled-test posture — tester v7 ran the C# 4089/0 + plang 273/273 (271 pass, 2 skipped) deterministically; HasSkipTag implementation reviewed in v7 (minor: lacks regression test, but the skipped-arm path is observed in tester actions). Accepting that gap matches the "honest skipped" memory pattern.

## Next bot

```
VERDICT: PASS
Next: run.ps1 auditor collections-are-data "Review the code on branch collections-are-data" -b collections-are-data
```
