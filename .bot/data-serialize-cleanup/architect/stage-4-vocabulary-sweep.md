# Stage 4: Vocabulary sweep — drop "envelope", rename `this.Envelope.cs`

**Goal:** Remove the word "envelope" from PLang's serialization vocabulary. Data is not enveloped in anything — Data IS the shape, full stop. Rename `this.Envelope.cs` and scrub the word from comments across the codebase.

**Scope:**
- `PLang/app/data/this.Envelope.cs` → renamed to `this.Transport.cs`. The methods inside (Wrap, Compress, Encrypt, Decrypt, Decompress, Unwrap) are transport-pipeline operations; "Transport" captures it.
- `PLang/app/data/this.Envelope.cs:11` and similar doc comments mentioning "envelope" — rewritten.
- `PLang/app/modules/signing/Signature.cs:9` — `"The signed data envelope. Owns creation (signing) and verification."` → `"Cryptographic signature attached to a Data. Owns creation (signing) and verification."`
- Any other `*.cs` file containing the word "envelope" in comments or identifier names — audit and rewrite.
- The `Wrap()` method's `var envelope = new @this(...)` local variable — rename to `var outer` or `var wrapped`.

**Out of scope:**
- Behavioural changes (this stage is pure vocabulary).
- Renaming `Wrap` / `Unwrap` themselves — those describe what the operations do (category wrapping), not "envelope-ing." They stay.

**Deliverables:**

File rename:
```
PLang/app/data/this.Envelope.cs  →  PLang/app/data/this.Transport.cs
```

Doc comment rewrites — examples:

`this.Envelope.cs:11-14` (before):
```csharp
/// <summary>
/// Data — envelope/transport concern.
/// Signature and Verified properties for wire integrity.
/// Pipeline methods: Wrap, Compress, Encrypt (outbound) and Decrypt, Decompress, Unwrap (inbound).
/// </summary>
```

After:
```csharp
/// <summary>
/// Data — transport-pipeline concern.
/// Signature property for wire integrity.
/// Pipeline methods: Wrap, Compress, Encrypt (outbound) and Decrypt, Decompress, Unwrap (inbound).
/// </summary>
```

`Signature.cs:9` (before):
```csharp
/// <summary>
/// The signed data envelope. Owns creation (signing) and verification.
/// All fields are serialized in a deterministic order for signature verification.
/// </summary>
```

After:
```csharp
/// <summary>
/// Cryptographic signature attached to a Data. Owns creation (signing) and verification.
/// All fields are serialized in a deterministic order for signature verification.
/// </summary>
```

Identifier rename inside `Compress()` / `Wrap()` (no public API change):
```csharp
// before
var envelope = new @this("", this, type.FromName(kind));
// after
var outer = new @this("", this, type.FromName(kind));
```

**Dependencies:** None strictly. Easier to land after Stage 2 (which removes the `Envelope` class wholesale) so the audit surface is smaller.

## Design

**Why this isn't bike-shedding.** The word "envelope" was an active conceptual leak. It implied Data lived inside a container, which made readers reach for parallel container types (the deleted `Envelope` class in `plang/Data.cs` is the load-bearing example). Removing the word removes the mental hook that recreates the workaround.

**What to keep.** `Wrap` / `Unwrap` describe an actual operation: taking a Data and creating an outer Data whose type names the category and whose value is the inner Data. That's category wrapping, not enveloping. The methods stay.

**What changes.** "Envelope" in:
- File names (`this.Envelope.cs` → `this.Transport.cs`)
- Doc comments anywhere it shows up
- Local variable names (`var envelope = ...` → `var outer` or contextual name)
- The `_envelopeJsonOptions` field in `this.Transport.cs` (formerly Envelope.cs) — already going away when Stage 2 lands the serializer composition, but if any vestige remains, rename to `_transportJsonOptions`.

**Audit method.** A simple grep across `PLang/**/*.cs` for "envelope" (case-insensitive) — fix every hit unless it's in a third-party reference or a historical CLAUDE.md proposal.

**CLAUDE.md proposal.** Worth adding to `/workspace/plang/.bot/data-serialize-cleanup/claude-md-proposals.md`:

```markdown
## architect — v1 — 2026-05-26
**Target:** /PLang/App/CLAUDE.md
**Why:** "Envelope" was an overloaded word that led to a parallel wrapper class (deleted in this branch's Stage 2). Future contributors should not reintroduce the concept.
**Proposed change:**
- **Data is not enveloped.** Data IS the wire shape — `{name, type, value, signature}`. Do not introduce parallel wrapper types ("Envelope", "Wire", "Wrapper") for Data's serialization shape; the wire shape is Data's own shape with `[Out]` filtering. If you find yourself building a parallel type to bypass `[JsonIgnore]`, the right answer is to add an `[Out]`-aware filter and let the existing `app.data.Json` converter handle the wire layout.
```

**Risks:** Minimal. The rename is mechanical. Worst case is a Git-history tracking hassle for `this.Envelope.cs` → `this.Transport.cs`; use `git mv` to preserve history.

**What the coder verifies:**
- `git grep -i envelope` in `PLang/**/*.cs` returns only intentional historical references (e.g., third-party comments).
- All projects build.
- All existing tests pass — this is pure rename.
- The CLAUDE.md proposal is appended to `claude-md-proposals.md`.
