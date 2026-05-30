# Stage 3: Kind derivation and canonicalisation

**Goal:** Derive the kind from the file extension at build, accept both long and short kind forms (`md|markdown`, `jpg|jpeg`) and normalise them to the extension, and rename the formats registry's "kind"→"name" so the codebase stops using "kind" for two meanings.
**Scope:** Included — the kind canonicaliser (derived from the formats registry), the `text.Build` wiring into `NormalizeParameterTypes` (already calls `Kinds.Of`), and the `app/formats/this.cs` rename. Excluded — strict enforcement (stage 4), the LLM block (stage 5).
**Deliverables:** A kind-canonicalisation function (extension-canonical, alias table derived from the registry, unknown kinds pass through); `text`'s extension-derived kind flowing through the existing `Kinds.Of` build path; `formats` renamed (`KindOf`→family/`NameOf`, `_extensionToKind`→family map).
**Dependencies:** Stage 2 (the `text` type and its `Build` hook).

## Design

See [plan/kind-derivation-and-validation.md](plan/kind-derivation-and-validation.md). Key points:

- **Canonical kind = the file extension, lowercased, no dot** (`md`, `jpg`, `mp4`). This is what `image.Build` already returns; `text.Build` (stage 2) produces the same shape.
- **The alias table is derived, not hand-written.** The formats registry already maps extension→MIME (`.md`→`text/markdown`). Invert the subtype (`markdown`, `jpeg`) → extension to get the accepted-aliases map; the extension maps to itself. When two extensions share a subtype (`.jpg`/`.jpeg` → `image/jpeg`), pick the primary (`jpg`). Unknown free-string kinds (no registry entry) pass through unchanged.
- Normalisation runs on the **build/LLM-facing path** — the `type` factory from stage 1 calls it. Runtime reads the already-canonical kind, never re-normalises.
- **The formats rename.** `app/formats/this.cs` calls the family the "kind" (`KindOf`, `_extensionToKind`, `_allKinds`). Under this model that set is the *name* vocabulary and the subtype is the kind. Rename so the new code reads right — `KindOf` → `FamilyOf`/`NameOf`, `_extensionToKind` → an extension→family map. Mechanical, but do it here so stages 4–5 build on consistent vocabulary. Update the `type.Compressible` derivation (it keyed off the old family-"kind").
