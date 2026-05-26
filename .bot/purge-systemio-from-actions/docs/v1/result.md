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

## Findings filed for other bots

None. Auditor v2 PASS, coder stage 7 docs already shipped the full
`good_to_know.md` System.IO section, and all new public surfaces (PathHelper,
PLNG002 analyzer, the new verbs, Execute permission, JsonConverter) ship
with adequate XML doc comments. No PLang `.goal` examples missing — this
branch was a security refactor of internal C#, not a new user-facing
PLang feature.

## Verdict

**PASS** — branch is doc-complete and ready to merge into `runtime2`.
