## docs — v1 — 2026-06-20

**Target:** `/workspace/plang/CLAUDE.md`

**Why:** Two new cross-cutting runtime rules landed on this branch that are easy to get wrong when writing new C# handlers or wire code. Neither is derivable from reading the changed files in isolation — both require understanding why the design changed and what the contract is now.

**Proposed change:**

```
- **`Wire.Read` auto-verifies `@schema:signature` layers on transport reads.** When the first property of a deserialized object is `@schema:"signature"`, `Wire.Read` runs `ReadSignatureLayer`, which verifies the signature inline and either peels to the inner `Data` or fails the read. Context-less Wire with `View.Out` fails **closed** — returns `SignatureVerifyContextMissing` error, never silently unwraps. Context-less Wire with `View.Store` trusts on read (local-FS threat model; verify-with-context is a tracked open item under SettingsStore refactor). The prior contract ("Wire.Read does not auto-verify") is obsolete as of `template-stamping-at-read`. Full contract: `Documentation/v0.2/wire-serialization.md` "Wire.Read auto-verifies…".

- **Born-typed variable decline.** `app.variable.@this.Create(value, asking)` is a pass-through: if `value` is already a `variable.@this`, it is returned; otherwise `asking.Fail("CreateDeclined", 400)` fires and the step fails. A variable NAMES a slot — it is born at the wire boundary when a `type:variable` param is read (`Variable.Resolve`). Never attempt to construct a variable from a value of another type; the generator-emitted guard catches it. Correct pattern: `declare 'type:variable'` in the action catalog entry so the source generator routes the raw name through `Variable.Resolve` rather than `Create`. Full rule: `Documentation/v0.2/variables.md` "Born-typed variable decline".
```
