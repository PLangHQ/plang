# Stage 1: `[Out]` Discipline + `RawSignature` Cleanup

> **Note for coder:** every code snippet, file path, type signature, and method name in this file is a **suggestion** that captures architect intent — not a contract. You own the implementation. Reshape names, restructure layout, or replace approaches as the real constraints demand. Push back on the design itself if you find it wrong.

**Goal:** Apply `[Out]` to every public property on the 13 in-scope domain types, per [`plan/wire-out-attributes.md`](plan/wire-out-attributes.md). Delete `Data.RawSignature`. Pure surface-prep stage — no `Normalize`, no new format encoders. The downstream stages assume `[Out]` is the only thing the wire serializer needs to consult.

**Scope:**
- Property attribute application on the 13 types listed in `plan/wire-out-attributes.md` (Identity, path / FilePath / HttpPath, list, Variable, Data, StatInfo, GoalCall, permission, setting, http.Response, Ask, Mock, condition.Operator).
- Deletion of `Data.RawSignature` (PLang/app/data/this.Envelope.cs:57–58) and migration of its four callers to use `Signature` directly.

**Out of scope:**
- `Data.Normalize()` (Stage 2).
- The `IWriter` protocol or any new format adapter (Stage 2 / 4).
- Replacing `path.JsonConverter` (Stage 2).
- Rewriting `As<T>` (Stage 3).
- Touching anything outside the 13 types unless it's a direct caller of `RawSignature`.

**Deliverables:**

1. **`[Out]` attributes applied** per the wire-out-attributes table. Use the existing `[Out]` attribute from `PLang/app/View.cs`. Properties that the table marks Skip get no `[Out]` (they're already invisible to the wire whitelist). Properties already marked `[Sensitive]` or `[JsonIgnore]` stay as-is.
2. **`Data.RawSignature` deleted.** Four callers migrated to `Signature`:
   - `PLang/app/channels/serializers/serializer/plang/Data.cs:50`
   - `PLang/app/channels/serializers/serializer/plang/Data.cs:121`
   - `PLang/app/modules/signing/code/Ed25519.cs:65,68`
   - `PLang/app/actor/permission/this.cs:94,139`
3. **`setting` shape: name visible, value masked.** `setting.key` gets `[Out]` (name travels for diagnostics — receivers can know what settings are configured). `setting.value` gets `[Out, Masked]` — name travels, value is replaced with `"****"` on the wire. This requires a **new `[Masked]` attribute** in `PLang/app/View.cs` (joins the existing `[Out]`, `[Sensitive]`, `[In]`, `[Store]`, `[LlmBuilder]`, `[Debug]`, `[Default]` cluster). The attribute itself is just a marker; the masking *behavior* is implemented in Stage 2's Normalize walker. For Stage 1, you only add the attribute class + tag `setting.value` with it.
4. **No behavior change yet.** The existing JSON serializer still works because `[Out]` is already what the JSON path consults. `[Masked]` is a marker only at this stage — the wire serializer doesn't yet honor it (Stage 2 picks that up). This stage is "pin everything down so Stage 2 has a stable surface to build on."

## Design

The wire-out-attributes doc is the source of truth for per-property decisions — read it in full before applying. Key principles you'll see reflected there:

- **Portable beats local.** path's `Relative` ships, `Absolute` doesn't (leaks filesystem layout).
- **Derived skips.** If a property is computable from another that already ships (`Extension` ← `Relative`), don't ship it.
- **Local management state skips.** `IsDefault`, `IsArchived`, runtime caches, parser breadcrumbs — none of it.
- **Settings travel with masked values.** `key` is visible, `value` is `"****"` on the wire. The `[Masked]` attribute is new — Stage 1 defines it, Stage 2 honors it in Normalize.

If a per-property decision in the table feels wrong once you're in the code (the rationale doesn't hold, the property doesn't exist anymore, a new property has appeared since the inventory), make the call yourself and note it — the architect will fold it back into the doc on the next pass.

**On `RawSignature`:** the docstring on the property itself confirms it's legacy (`"after stage 2a.7, ICallback is gone — no auto-populate on read"`). It's now a duplicate accessor with identical semantics to `Signature.get`. Deletion + 4 caller migrations is mechanical; verify each caller works with `Signature` directly (they all currently grab a `Signature?`, so a rename is the whole change).
