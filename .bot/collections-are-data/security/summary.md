# Security — collections-are-data

**Version:** v1
**Verdict:** PASS · **Next bot:** auditor

## What this is

`collections-are-data` reshapes list/dict into native Data-holding containers (chunk/row list model with `CopyStructure` on the merge boundary, dict as ordered key→Data store), introduces a new `where` action, and makes Data self-identify on the wire via a `@schema:"data"` marker (replacing the old name+value shape-sniff recognizer). Predecessor reviews: codeanalyzer v4 PASS, tester v7 PASS (271 pass + 2 honestly Skipped).

## What was done

Independent security review, threat-modelled at PLang's user-sovereign boundary (signature-verified Data = trusted; defend the wire/external-data path, not user actions). Audited the four risk areas the diff introduced:

1. **Wire `@schema` marker** (Wire.cs / data/this.cs UnwrapJsonElement lift) — clean. Strict marker, MaxJsonDepth=128 cap, strict-kind validator in variable.set runs post-lift.
2. **list/dict native types** — clean. CopyStructure aliasing fix verified by codeanalyzer v4's permanent regression test. Two informational lows below.
3. **`where` action** — clean. Field is a name not an expression, Operator is typed, no injection.
4. **F2 signing regression** — fail-closed denial-of-trust, NOT signature forgery. Reframed to medium accepted-risk under the existing merge gate.

Wrote 3 findings (1 medium accepted-risk, 2 low open) to `.bot/collections-are-data/security-report.json`. Verdict + result in `v1/`.

**Files reviewed:** `PLang/app/data/Wire.cs`, `PLang/app/data/this.cs`, `PLang/app/module/signing/code/Ed25519.cs`, `PLang/app/type/list/this.cs`, `PLang/app/type/dict/this.cs`, `PLang/app/type/text/this.Convert.cs`, `PLang/app/module/list/where.cs`, `Tests/LazyDeserialize/Signed*.test.goal`.

## Merge gate (carries from codeanalyzer v4)

Do NOT merge `collections-are-data` to `main` ahead of sibling branch `signature-as-schema-wrapper`. F1 ships a verify-side regression for signed Data through list/goal — fail-closed, not exploitable, but real UX/correctness breakage for legitimate users.

## Findings shape

- **F1 (medium, accepted-risk)** — verify-through-list / verify-through-goal returns false because Data round-trips as Data{value: Data{value: …}} and `Ed25519.VerifyAsync` step 7 rehashes the outer. Fail-closed; deferred to `signature-as-schema-wrapper`.
- **F2 (low, open)** — `text/this.Convert.cs:32` calls `JsonSerializer.Serialize(value)` with NO options on the native dict/list/IEnumerable arm. Loses `[Sensitive]` redactor. Reach today is narrow (Identity.PrivateKey via `set %list% = [%identity%]; set %t% = %list% as text`).
- **F3 (low, open)** — `list.@this.CopyStructure()` has no depth guard. Bounded today by `MaxJsonDepth=128` on the wire path; latent the moment any new lift path loses that cap.

## Code example (the F2 leak shape)

```csharp
// PLang/app/type/text/this.Convert.cs:32 — current
return global::app.data.@this.Ok(JsonSerializer.Serialize(value));

// fix — pass options so [Sensitive] redactor + IgnoreCycles apply
return global::app.data.@this.Ok(
    JsonSerializer.Serialize(value, Conversion.ContextualReadOptions(context)));
```
