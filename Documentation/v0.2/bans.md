# Production Guardrails — Bans & Limits

> Decomposed out of `good_to_know.md` (2026-05-31). Content moved **verbatim** — stale pre-rename names are tracked in the `good_to_know.md` index under "Known stale references", not yet swept.

## Security Hardening — Defense-in-Depth Limits

Several subsystems have resource limits to prevent abuse:

| Subsystem | Guard | Limit |
|-----------|-------|-------|
| **HTTP downloads** | `MaxDownloadSize` | 100MB (configurable) |
| **HTTP in-memory reads** | `ReadLimitedStringAsync` / `ReadLimitedBytesAsync` | 100MB |
| **HTTP SSE** | Consecutive overflow counter | Disconnect after 3 |
| **HTTP all streams** | Throughput floor | 1KB/sec over 30s (slow-loris protection) |
| **HTTP URL scheme** | `ResolveUrl` | Only `http://` and `https://` |
| **JSON navigation** | `MaxElementCount` | 100,000 elements |
| **JSON navigation** | `MaxDepth` | 64 levels |
| **JSON string parse** | `MaxJsonStringSize` | 10MB |
| **Variable resolution** | `ResolveDeep` breadth | 100,000 items |
| **Variable resolution** | `ResolveDeep` depth | 100 levels |
| **Ed25519 verification** | Header comparison | Constant-time via `CryptographicOperations.FixedTimeEquals` |
| **File errors** | Error messages | No absolute paths exposed |

---

## System.IO Is Banned in Production C# (use `path.@this`)

Action handlers and engine code under `PLang/app/**` must NOT call
`System.IO.*` directly. The only allowed filesystem surface is the
`app.type.path.@this` verb set (`ReadText`, `ReadBytes`, `WriteText`,
`WriteBytes`, `Append`, `Mkdir`, `Delete`, `List`, `Stat`, `MoveTo`,
`CopyTo`, `ExistsAsync`, `AsBooleanAsync`). Every one of those methods
passes through `FilePath.AuthGate(verb)` before touching the disk.

A handler reaching for `System.IO.File`, `System.IO.Directory`,
`System.IO.FileInfo`, or `System.IO.Path.*` (Combine/GetDirectoryName/
GetFullPath/...) is reaching **under** the auth gate. That means an
out-of-root path the actor never consented to gets read / written
silently. It's the filesystem analogue of the `Console.*` ban below.

**The rule.** A filesystem path in interior C# is `app.type.path.@this`
(the lowercase `path` alias). `string` only appears at the perimeter:
CLI args, JSON-on-disk shape (the wire format), scheme-resolved DLL
loads, and the App root anchors (`App.AbsolutePath`, `App.OsDirectory`,
`App.OsAbsolutePath` — they define the root, so they can't be lifted).
Crossing the perimeter into memory means calling
`path.Resolve(rawString, context)`.

**Build-time gate (PLNG002).** The PLang source generator emits a
`PLNG002` diagnostic — **at error severity** — on every `System.IO.*`
member-access reach that touches the disk, plus every `Data<string>`
action-handler property named `Path` / `PrPath` / `Source` /
`Destination` / `Directory` / `Folder` / `FilePath` under `PLang/app/**`.
A clean build is the bar — the codebase has zero PLNG002 warnings as of
the `purge-systemio-from-actions` branch landing.

Allowlist (pure name math, separator constants — none touch the
filesystem): `System.IO.Path.DirectorySeparatorChar` /
`AltDirectorySeparatorChar` / `PathSeparator` / `VolumeSeparatorChar`,
plus `Path.Combine` / `GetDirectoryName` / `GetFileName` /
`GetFileNameWithoutExtension` / `GetExtension` / `GetRelativePath` /
`ChangeExtension` / `GetInvalidFileNameChars` / `GetInvalidPathChars` /
`HasExtension` / `IsPathRooted` / `IsPathFullyQualified` /
`GetFullPath` / `Join` / `TrimEndingDirectorySeparator` / `GetPathRoot` /
`EndsInDirectorySeparator`. These are string transformations, not IO.

Exempt files / namespaces: `app.type.path.**` (the verb surface
legitimately uses `System.IO` post-AuthGate); the `PLang.Generators`
project; and `app.module.MarkdownTeaching` (bootstrap-time discovery of
static repo-shipped teaching .md files — converting its sync utility
shape to async-everywhere buys no security and lots of churn).

**`.Absolute` discipline (D13).** `path.Absolute` is an easy-to-misuse
escape hatch. Any reach for `.Absolute` outside `app.type.path.**`
means a third-party API (sqlite, image library, `Assembly.LoadFrom`) is
about to touch the filesystem with no gate. Handlers MUST `await
path.Authorize(verb)` first and check `auth.Success` before reading
`.Absolute`. The verb surface (`ReadText`, `WriteText`, `List`, `Stat`,
`ReadBytes`, ...) does this automatically — reach for verbs first;
only fall through to `.Absolute` + manual `Authorize` when a
third-party API genuinely takes over the file (D9b — sqlite is the
canonical case).

**Migration status (purge-systemio-from-actions branch — landed).**

- Stage 1 — derivation verbs (`Parent`/`WithName`/`WithExtension`/
  `Combine`/`InFolder`) + PLNG002 analyzer.
- Stage 2 — `.goal` MIME → Goal deserialization (FilePath.ReadText
  parses `.goal` via Goal.Parse, stamps Path back-reference).
- Stage 3 — Goal.Path / PrPath / LoadedFromPrPath / GoalCall.PrPath
  flip to path?. JSON converter takes Context in its constructor;
  per-Actor `channels.serializers` instances bake a Context-bound
  converter into their options. `Conversion.TryConvertTo(value, type,
  context)` builds a one-shot Context-bound options bag per call so
  deserialised Path fields land Context-wired immediately.
- Stage 4 — AppGoals path-keyed dicts (separate `_byName` index for
  fuzzy lookups); App.Load/Save through path verbs.
- Stage 5 — full ring-2 handler sweep. `test/discover` (the brief's
  headline), `test/report`, `settings/Sqlite` (D9b take-over), `llm/OpenAi`
  (D9a content-shape: `path.ReadAsDataUri`), `module/add` + `code/load` +
  `code/Snapshot` (D8: new `Execute` verb + `path.LoadAssemblyAsync`),
  `ui/Fluid` + `http/Default` file providers, `debug` trace writes
  (`path.Append` + derivation chain), `modules.this.MarkdownTeachingRoot`,
  `goals.LoadFromFileAsync` / `LoadFromDirectoryAsync` / `TryLoadPr` /
  `GetByPrPathAsync`, `goals.goal.Methods.FormatForLlm`,
  `modules.builder.RunAsync` (app.pr existence probe),
  `modules.builder.goals` / `modules.builder.load` (action Path slots).
  New permission verb `Execute` distinct from Read (Unix r/w/x model).
- Stage 6 — PLNG002 flipped to `DiagnosticSeverity.Error`. PLang and
  PlangConsole build clean with zero PLNG002 warnings. The gate now
  fails compilation on regression.

## Console.* Is Banned in Production C#

Channels exist so that I/O is **redirectable** — a user can re-register the `output`/`error`/`debug` channel to a file, an in-memory buffer, an HTTP response, or a goal. Any `Console.WriteLine` / `Console.Write` / `Console.Error.WriteLine` in production C# silently bypasses that surface and breaks the contract.

The rule, with the three flavours of write:

- **Diagnostic / debug chatter** (lifecycle banners, `--debug` traces, internal warnings) → `await context.App.Debug.Write(...)`. This routes through the `debug` channel falling back to `error`, and is gated on `IsEnabled` so it goes silent without `--debug`. Sites that subscribe as `Action<...>` (sync event handlers) can use `_ = Debug.Write(...)` — `Console.Error` was non-awaitable already, so ordering guarantees don't change.
- **User-facing program output** (builder progress lines, LLM validation chatter — the user expects to see them with `--debug` off) → `await app.CurrentActor.Channels.WriteTextAsync(global::app.channel.@this.Output, ...)`. Do **not** route these through `Debug.Write` — the `IsEnabled` gate would silence them in the default case.
- **Interactive prompts** (the App build "create new app? (y/n)" prompt is the canonical example). The default console pair is direction-split: `output` is write-only, `input` is read-only. `Channel.Stream.AskCore` writes-then-reads on a single bidirectional stream and does not work across the split pair. Two-call pattern: write the prompt through `output`, then `using var reader = new StreamReader(inputChannel.Stream, leaveOpen: true)` and `await reader.ReadLineAsync()`. Don't extract a `Channels.AskAcrossAsync` primitive on speculation — there's only one caller.

The two `Console.*` references that **stay**:

- `Console.IsInputRedirected` / `Console.IsOutputRedirected` — these are **queries** ("is stdin a TTY?"), not writes. They gate *whether* to prompt, not *how*.
- `PlangConsole/Program.cs:26` — the process boundary. If `executor.Run` itself fails before channels are wired, this is the last-resort error sink. Single explicit exemption.

Test fixtures that capture stderr by `Console.SetError(...)` are broken under the channel model — the `error` channel was registered with `Console.OpenStandardError()` *at boot*, and re-pointing `Console.Error` later doesn't affect the captured Stream reference. Capture by registering a memory channel as `"error"` on the System actor instead — that's the redirection model channels exist to provide.
