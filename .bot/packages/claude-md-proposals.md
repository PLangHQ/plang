## architect — packages 3 — 2026-10-25
**Target:** `CLAUDE.md`, the "Owners take values; handlers open carriers" bullet (Runtime2 Conventions)
**Why:** the bullet came from my decision 231 (app-systems, 2026-09-28), which Ingi never ruled. On 2026-10-17 (decision 851) Ingi rejected it: "yes, this is a bad rule. data should flow as far as it can". The replacement was proposed on kind-is-type (`.bot/kind-is-type/claude-md-proposals.md`, architect v2), but the bullet is still in CLAUDE.md:21, so every handler is still taught to open its slots, the opposite of the rule. Found again by plang-docs in the todos.md triage (entry 1562).
**Proposed change:** replace the bullet with:

```
- **Data flows as far as it can.** A carrier (`data<T>`) is passed on whole — to an owner, a value, a constructor — and opened only by whoever needs the value, when it needs it. Don't open a slot to hand its value on, and don't open it because a later owner will need it (Ingi, decision 851). An owner's operation never takes CLR primitives (`string`, `bool`, `int`); the CLR lowering happens only at the real boundary inside the owner (the System.IO call, the .NET API).
```
