# codeanalyzer v1 — app-lowercase

## Scope

18 commits on top of `runtime2`. Two distinct workstreams:

1. **Mechanical rename sweep** (Phases 1–4 + Builder): `App` → `app` root, `Data/Type/Path` leaf renames, vocabulary sweep across ~22 subfolders. Cosmetic by design — no semantic change.
2. **Seven OBP merges** (Cache, Builder, Callback, Settings, Modules, Code, Debug): collapse case-pair folders (`app/Cache/` vs `app/modules/cache/`) into a single namespace. Originally scoped *out* of the rename branch by the architect, but pulled in interactively per Ingi.

This second workstream is where the codeanalyzer earns its keep. The rename itself is hard to get wrong silently — the compiler enforces it. The OBP merges are a real shape change and need verification.

## Approach

Five passes per the character file, but weighted: heavy on the merges, sample-only on the rename diff.

### Pass 1 — OBP compliance (merge-focused)

For each of the 7 merged folders, verify:
- Single `@this` per folder (not two registries-pretending-to-be-one).
- No leftover orphan files from the source side of the merge.
- Mutation discipline now lives inside the type, not at the call site.
- Smell #3 (same logical thing stored twice) is actually resolved.

Spot-check the rest of `app/` for shape smells introduced by the namespace move (e.g. the `Default` carve-out subfolder, the `environment`/`load` renames).

### Pass 2–3 — Simplification + readability

- The StoreOnlyModifier bug fix: did it close cleanly or open a new shape?
- The `Default` PascalCase carve-out: documented? Reasoned?
- The `app.run` → `environment.run` and `builder.app` → `builder.load` breaking renames: do the type names make sense after the move, or are they placeholders that read as code-smell?
- Generator string literals: are any still PascalCase by mistake?

### Pass 4 — Behavioral

- Reflection / attribute-name / string-literal trails — anything still saying `"App."` that needs to say `"app."`.
- Case-insensitive FS portability of the final tree — does any pair of files differ only in case at the root of `app/`?
- The `PLang.Tests/GlobalUsings.cs` aliases that the rename was supposed to retire — were they actually retired, or are they still there as legacy?

### Pass 5 — Deletion test

- Any tombstone files / empty namespace `this.cs` shells left behind by the merges?
- Any new helper introduced *for the rename* that no longer earns its keep?

### Pass 6 — Build + test sanity

Clean rebuild per CLAUDE.md "stale-binary trap" protocol, then both suites.

## Output

- `report.md` with per-file findings (severity-ordered).
- `verdict.json`.
- `summary.md` at bot root.
- Append session to `.bot/app-lowercase/report.json`.

## Non-goals

- Re-reviewing the mechanical rename file-by-file. The architect's plan + coder's exit summary already pin the shape; spot-checking representative files is enough.
- Editing source. Codeanalyzer is read-only.
