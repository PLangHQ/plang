# Auditor v2 — result (singular-namespaces)

## Verdict: **PASS** (unchanged, with v1 F1 retracted)

Security v1 PASS landed at `179121964` after my v1 ran (I hadn't fetched). Their
3 low residuals reinforce — they don't change — my v1's substantive findings.

## Security v1 cross-reference

| security finding | overlaps my v1 finding | notes |
|---|---|---|
| F1 `_context = null!` discipline fragile | F5 (producer-stamping invariant) | Same load-bearing convention from two angles. Security: future `_context.X` deref → NRE on wire-deserialized Data. Mine: `Data.Context` must be set before `Data.Type.Fields` read. Both want a class-invariant note in `good_to_know.md`. Two-reviewer agreement = real shape, deserves the doc. |
| F2 Channel handler null-context force | (none) | Service-owned channels force `binding.Handler(context!, ...)`. Latent, not exploitable. Genuinely a security-shaped find I didn't catch. |
| F3 `Wire.Read` doesn't stamp Context | F5 (producer-stamping invariant) | Same convention again — wire ingest leaves Context unstamped; caller's job. Reinforces the docs ask. |

## What changes from v1

- **F1 retracted** — "no security review on branch" was a stale-state finding.
  Security existed; I hadn't fetched. Captured the lesson as `feedback-upstream-bots-required`
  memory + character proposal.
- **F2-F5 stand** — codeanalyzer-v4 latent items (`IsNull` magic string,
  `As(string)` fallback drop, `Scheme` NRE) still in HEAD; producer-stamping
  invariant load-bearing across module boundaries (security's F1 + F3 echo this).
- **Net finding count: 4** (was 5).

## Next

Security routed to docs; I agree.

```
run.ps1 docs singular-namespaces "Write documentation for the changes on branch singular-namespaces" -b singular-namespaces
```

Docs has three concrete asks pulling the same direction:
1. **Producer-stamping invariant** in `good_to_know.md`: every Data must have
   `Context` stamped before any consumer reads `Data.Type.Fields/Values/Example`,
   and that the same convention covers `_context.X` derefs and `Wire.Read`
   call-sites.
2. **`type.Null` sentinel** — document its identity (singleton) so the
   `IsNull => Value == "null"` line stops looking like the intended check.
3. **`Promote()` throw** — document it as the architect's "fail-loud at source"
   contract, with the "stamp before read" rule.
