# docs — purge-systemio-from-actions — v1 result

## CHANGELOG — user-visible runtime changes

Anyone consuming PLang Runtime2 externally should know about these.

### Security model

- **`System.IO` is now banned in PLang/app/** by build-time analyzer
  PLNG002 at **Error** severity. Forks carrying direct `System.IO.File` /
  `System.IO.Directory` / `System.IO.FileInfo` / `System.IO.Path.*` reaches
  in production C# under `PLang/app/**` will fail to compile.
  - **Allowlist** (pure name math, no IO): `Path.DirectorySeparatorChar`,
    `AltDirectorySeparatorChar`, `PathSeparator`, `VolumeSeparatorChar`,
    `Path.Combine`/`GetDirectoryName`/`GetFileName`/`GetFileNameWithoutExtension`/`GetExtension`/`GetRelativePath`/`ChangeExtension`/`GetInvalidFileNameChars`/`GetInvalidPathChars`/`HasExtension`/`IsPathRooted`/`IsPathFullyQualified`/`GetFullPath`/`Join`/`TrimEndingDirectorySeparator`/`GetPathRoot`/`EndsInDirectorySeparator`.
  - **File-scope exemptions:** `app.types.path.**` (the verb surface
    legitimately uses System.IO post-AuthGate), `PLang.Generators`,
    `app.modules.MarkdownTeaching` (bootstrap-time discovery of
    repo-shipped static teaching files).
  - **The route to a permission prompt is now compilation-enforced**:
    every filesystem reach in production C# goes through `path.@this`
    verbs, which gate via `FilePath.AuthGate(verb)`.

### New permission verb

- **`app.types.path.permission.verb.Execute`** — distinct from Read,
  mirroring the Unix r/w/x model. Granted when a path is to be loaded as
  code (DLL via `Assembly.LoadFrom`, scripts handed to an interpreter).
  `AllowAll()` and `Covers()` updated to include Execute. The Authorize
  prompt label renders as "execute" — distinct from "read" prompts.

### New verbs on `path.@this`

All gated, all compose on top of existing verbs (no new bypass surface).

- **`LoadAssemblyAsync()`** — `Task<Data<Assembly>>`. FilePath calls
  `AuthGate(Execute)` then `Assembly.LoadFrom(Absolute)`. Non-FS schemes
  return `Fail("NotSupported", 400)`.
- **`ReadAsBase64()`** — `Task<Data<string>>`. `ReadBytes` (gated) +
  base64. Use site: OpenAI image attachments, sealing binary payloads
  into JSON-only transports.
- **`ReadAsDataUri()`** — `Task<Data<string>>`. `ReadBytes` (gated) +
  base64 + `data:<mime>;base64,` prefix. Use sites: embedded image tags,
  mail attachments, any wire payload that wants self-contained binary.
- **Derivation verbs** (already in stage 1, called out here for
  completeness): `Parent`, `WithName`, `WithExtension`, `Combine`,
  `InFolder`, plus filesystem-specific overrides on `FilePath`.

### Wire format / model changes

- **`Goal.Path`, `Goal.PrPath`, `Goal.LoadedFromPrPath`, `GoalCall.PrPath`
  are now `path.@this?`** instead of `string`. JSON shape on disk is
  unchanged — `PathJsonConverter` (re)serializes the portable
  `Relative` string. Consumers that read `.goal` JSON externally (other
  tools, custom dashboards) see the same wire shape as before, but C#
  code that touched these properties as strings needs to update.
- **`PathJsonConverter` takes `actor.context.@this` in its constructor.**
  Per-Actor `channels.serializers` bakes a Context-bound converter into
  its options so deserialized Paths land scheme-correct and
  Context-wired immediately. The no-Context default ctor (the "stub"
  form) is what global Conversion uses when no caller supplied Context;
  it yields a bare file-scheme Path that will fail on first Authorize.
- **`Conversion.TryConvertTo(value, type, context)`** — third Context
  parameter. When non-null, builds a one-shot Context-bound options bag
  per call.

### App graph

- **`App.Parent`** — new optional back-reference. When an app is
  constructed as a child of another (e.g. the per-test child apps spun
  up by `test.run`), `Parent` carries the originating app and
  `FilePath.IsInRoot` walks the chain so the child inherits the
  parent's filesystem scope. `null` for top-level apps. Cycle-capped at
  depth 16.

### Tooling

- **`Documentation/v0.2/scripts/check-app-tree.sh`** — new drift checker
  for `Documentation/v0.2/app-tree.md`. Reports modules / `app.@this`
  properties / `actor.@this` properties / `app/data/this.*.cs` partials
  that exist in source but aren't mentioned in the doc (and vice
  versa). Carried over from runtime2; ran clean against this branch
  after the App.Parent / Snapshot partial / Schema-module-skip edits
  below.

## Documentation gaps filled in this version

| File | Change | Why |
|---|---|---|
| `CLAUDE.md` | Replaced stale PLNG002 parenthetical ("currently warning; flipping to error once...") with "at **error** severity — PLang and PlangConsole build clean with zero PLNG002 warnings as of the `purge-systemio-from-actions` merge". | Stage 6 landed; the one-liner was three sentences out of date. |
| `PLang/app/types/path/this.JsonConverter.cs` | Added one-line XML doc on both public constructors distinguishing "stub" (no Context) vs "Context-wired" forms. | Class-level summary has the full story; the ctors are the call sites — IntelliSense should surface the distinction without forcing the reader into the class summary. |
| `Documentation/v0.2/app-tree.md` | Added `App.Parent` to the top-level tree; added `Snapshot` (`this.Snapshot.cs`) to the Data partial list; added a paragraph explaining that `app.Modules.Schema` is the LLM action catalog (not a registered action module). | All three were real omissions surfaced by the drift checker; first two are new this branch, third was pre-existing. |
| `Documentation/v0.2/scripts/check-app-tree.sh` | Added `Schema` to the module-skip list with reason. | Schema is PascalCase infrastructure under `modules/`, not a vocabulary action module. Skip + documented narrative beats a forced bullet line. |

After all four edits, the drift checker reports clean (31 modules,
37 app props, 7 actor props, 5 data partials).

## Comment-rot cleanup (added mid-session per Ingi)

Ingi flagged a separate concern after the initial pass: comments in code
that reference review iterations / architect-plan items / Q-numbers /
D-numbers will read as gibberish six months from now ("XXX was decided
because Q1 was answered"). Did a sweep across the diff and across the
test suite. Patterns stripped:

- `Stage N — Batch M.` / `Stage N — ` summary prefixes on test classes
  (the class name already conveys what's being tested).
- `(D8/C5)`, `(D9a / C9)`, `(D13)`, `(D3, C7/C11)` doc-section references
  in production XML doc and inline comments.
- `(codeanalyzer v1 F3)`, `(codeanalyzer v2 N1)`, `Tester v7 N1`,
  `coder v6's fix`, `Hardened post tester v2:`, `tester v2 flagged`,
  `Auditor v1 F-A`, `Security v1 S3`, `Architect plan P8:` —
  review-iteration markers from prior bot passes.
- `Ingi C1`, `Ingi Q4`, `per Q4 decision`, `architect plan, "X section"` —
  alignment-meeting markers.

The technical substance under each label was preserved (constraints,
invariants, why a regression matters). What got dropped is the
provenance ("this came from review N"). Where a comment turned out to
just *be* a label with no substance left after the strip, the comment
was deleted entirely — the class name or method name carries the
information.

Production-code files touched (12): `PLang/app/goals/this.cs`,
`PLang/app/types/path/this.cs`, `PLang/app/types/path/this.Operations.cs`,
`PLang/app/types/path/file/this.Operations.cs`,
`PLang/app/types/path/http/this.cs`,
`PLang/app/types/path/http/this.Derivation.cs`,
`PLang/app/modules/llm/code/OpenAi.cs`,
`PLang/app/modules/test/report.cs`,
`PLang/app/modules/settings/Sqlite.cs`,
`PLang/app/modules/assert/code/Default.cs`,
`PLang/app/channels/serializers/serializer/plang/Data.cs`,
`PLang/app/data/this.cs`.

Test files touched: ~50, mostly stripping `Stage N — Batch M.` summary
prefixes from class-level `<summary>` blocks. C# (3031 build) and tests
build clean — 0 errors.

**Mechanism note:** ran the strip as a Python script over the diff, with
narrow regexes for each label family. First pass over-reached and stripped
some legitimate whitespace alignment — caught and reverted by re-applying
a guarded version of the script that only kept edits on lines that
actually matched a rot pattern. Final state verified by grepping for the
same rot patterns across `PLang/` + `PLang.Tests/` — none remain.

## Documentation freshness — `/shared/app-tree/` references removed

Ingi confirmed mid-session: the team has stopped maintaining
`/shared/app-tree/`. The in-repo `Documentation/v0.2/app-tree.md` is now
the canonical app tree. Removed the four references from app-tree.md
that framed it as a "summary in front of a deeper tree". The deep-dive
home is now the source itself (`PLang/app/**/this.cs`). Also updated
the coder-v1 CLAUDE.md proposal decision from **deferred** to
**rejected** — the proposal's stated "right home" (`/shared/app-tree/`)
no longer exists as a maintained doc; the intent (default-impl tracing
hop) belongs on XML doc on the abstract class itself, not on the runtime-
shape summary.

## Findings filed for other bots

None. Auditor v2 PASS, coder stage 7 docs already shipped the full
`good_to_know.md` System.IO section, and all new public surfaces (PathHelper,
PLNG002 analyzer, the new verbs, Execute permission, JsonConverter) ship
with adequate XML doc comments. No PLang `.goal` examples missing — this
branch was a security refactor of internal C#, not a new user-facing
PLang feature.

## Verdict

**PASS** — branch is doc-complete and ready to merge into `runtime2`.
