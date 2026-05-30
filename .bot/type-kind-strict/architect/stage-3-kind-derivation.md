# Stage 3: Kind derivation and canonicalisation

**Goal:** Derive the kind from the file extension at build, accept both long and short kind forms (`md|markdown`, `jpg|jpeg`) and normalise them to the extension, and rename the family-`Kind`/dispatcher uses so the codebase stops using "kind" for multiple meanings.
**Scope:** Included — the kind canonicaliser (derived from the format registry), the `text.Build` wiring into `NormalizeParameterTypes` (already calls the dispatcher), and the `App.Format.KindOf` + `App.Type.Kinds` renames. Excluded — strict enforcement (stage 4), the LLM block (stage 5).
**Deliverables:** A kind-canonicalisation function (extension-canonical, alias table derived from the registry, unknown kinds pass through); `text`'s extension-derived kind flowing through the existing dispatcher path; `App.Format.KindOf`→`FamilyOf`/`NameOf`; `App.Type.Kinds`→`App.Type.KindHooks`.
**Dependencies:** Stage 2 (the `text` type and its `Build` hook).

## Design

See [plan/kind-derivation-and-validation.md](plan/kind-derivation-and-validation.md). Key points:

- **Canonical kind = the file extension, lowercased, no dot** (`md`, `jpg`, `mp4`). This is what `image.Build` (`PLang/app/type/image/this.Build.cs`) already returns; `text.Build` (stage 2) produces the same shape.
- **The alias table is derived, not hand-written.** The format registry (`app.format.list.@this`, `PLang/app/format/list/this.cs`) already maps extension→MIME (`.md`→`text/markdown`). Invert the subtype (`markdown`, `jpeg`) → extension to get the accepted-aliases map; the extension maps to itself. When two extensions share a subtype (`.jpg`/`.jpeg` → `image/jpeg`), pick the primary (`jpg`). Unknown free-string kinds (no registry entry) pass through unchanged.
- Normalisation runs on the **build/LLM-facing path** — the `type` factory from stage 1 calls it. Runtime reads the already-canonical kind, never re-normalises.
- **Wiring:** `NormalizeParameterTypes` (`PLang/app/module/builder/code/Default.cs`, ~895) already stamps `p.Kind = App.Type.Kinds.Of(underlying, p.Value)` via the dispatcher `app.type.kind.@this`. Once `text` has its `Build` hook (stage 2), this path stamps `text` kinds with no further change beyond the rename.
- **The renames.** `app.format.list.@this` calls the family the "kind" (`KindOf`, `_extensionToKind`, `_allKinds`). Under this model that set is the *name* vocabulary and the subtype is the kind — rename `KindOf` → `FamilyOf`/`NameOf`, `_extensionToKind` → an extension→family map. And rename the build-hook dispatcher `App.Type.Kinds` (`app.type.kind.@this`) → `App.Type.KindHooks` so it stops colliding with the entity's `Kind`/`Kinds`. Mechanical, but do it here so stages 4–5 build on consistent vocabulary. Update `type.Compressible` (it keyed off the old family-`Kind`).
