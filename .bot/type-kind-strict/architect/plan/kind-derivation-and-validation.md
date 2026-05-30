# Topic — how a kind is set, canonicalised, and validated

Three questions: who sets the kind, what the canonical token is, and where strict bites.

## Who sets the kind

Two producers, never competing:

- **The LLM emits a kind** only when the step text names a format — "as markdown", "as image/gif", "write to %x%(csv)". It does not memorise vocabularies; it extracts from prose. It emits `name` and `kind` as separate fields of the `type` value.
- **The build derives a kind** from the file extension when the value is a path-like literal. This is already wired: `NormalizeParameterTypes` (in `builder/code/Default.cs`) calls `App.Types.Kinds.Of(declaredType, value)`, which invokes the type's `static string? Build(object?)` hook. `image.Build` already returns the extension (`photo.jpg` → `jpg`, `data:image/gif` → `gif`). Once `text` has a `Build` hook, `.md` literals auto-stamp `kind=md`.

So for a literal file path the dev needn't say anything — build fills the kind. For an explicit format intent the LLM fills it. For a bare string with no format, kind stays null (a hint that's absent).

## The canonical token

**The file extension, lowercased, no dot.** `md`, `html`, `jpg`, `mp4`. This is what "extrapolated from the extension" means and what `image.Build` already produces.

Both long and short forms are accepted on input and normalised to the extension at build:

- `markdown` → `md`, `jpeg` → `jpg`, `text/plain` → `txt` (the `text/` family resolves `name=text`, the subtype maps to its extension).

The alias table is **derived from the formats registry**, not hand-maintained. The registry already maps extension→MIME (`.md`→`text/markdown`, `.jpg`→`image/jpeg`). Inverting the subtype (`markdown`, `jpeg`) back to its extension gives the alias map; the extension maps to itself. When two extensions share a subtype (`.jpg` and `.jpeg` both → `image/jpeg`), the normaliser picks the primary (`jpg`). Unknown free-string kinds (no registry entry) pass through unchanged — kind is open, per the "free string when no definition" rule.

This normalisation lives in the `type` factory / the build validation step, called on the LLM-facing path. Runtime reads the already-canonical kind.

## Where strict is enforced

`strict` only has teeth where the family can verify the value. The split:

| Value form | When | What happens |
|------------|------|--------------|
| Literal, sniffable family (`image/gif strict`, value is a file/bytes literal) | build (`ValidateBuild`) | Read/sniff the value; compare actual format to required kind. **Match → ok. Mismatch → build error.** |
| Literal, unverifiable family (`text/md strict`) | build | No byte check possible. Only assert the kind name is known; otherwise pass. Strict is effectively a no-op for text. |
| `%var%` value, any family | runtime | Build can't see the value. Resolve at runtime; if the family is sniffable, verify and fail with a typed error on mismatch. |
| Default (not strict) | build | Stamp the derived/emitted kind as a hint. **Validate nothing.** A surprising kind may warrant a warning, never an error. |

This rides the **existing** `IBuildValidatable.ValidateBuild` seam — the same one `variable.set` already uses to check that a literal value converts to its declared type, and which already skips `%var%` (deferring to runtime). No new build pass. The per-type verification logic lives behind `IKindValidatable` (see [type-value-model](type-value-model.md)) so `build.validate` never grows a per-format switch — it asks the type.

Ingi's framing: "build could validate if the file exists — error on strict, warning on default." That's exactly this table: for a literal path with an extension, build derives the kind and, under strict, verifies; under default it stamps and at most warns.

## The formats-registry naming knot

The formats registry (`app/formats/this.cs`) calls the family the **kind** today: `KindOf`, `_extensionToKind`, `_allKinds = {text, image, video, ...}`. Under this model that set is the **name** vocabulary, and the subtype (`md`, `jpg`) is the kind. The registry should be renamed so the codebase stops using "kind" for two things — `_extensionToKind` → family/name mapping, `KindOf` → `FamilyOf` (or `NameOf`). This is mechanical but load-bearing for clarity; do it as part of stage 3 so the new code reads consistently.
