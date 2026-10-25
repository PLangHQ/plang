# Production Guardrails — Bans & Limits

> Part of the App architecture notes — index in [`good_to_know.md`](good_to_know.md).

## Security Hardening — Defense-in-Depth Limits

Several subsystems have resource limits to prevent abuse:

| Subsystem | Guard | Limit |
|-----------|-------|-------|
| **HTTP downloads** | `MaxDownloadSize` | 100 MiB, a size (configurable) |
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
| **Regex** | `NonBacktracking`, else a timeout | A timeout throw is answered at the action's and step's `FromException` (500, fails closed) |
| **Kept list** | Members in a canonical order (by name, ignoring case) | The digest never depends on insertion order |
| **Archive unpack** | A link whose target leaves its folder | Refused |

---

## Own settings and identity keys

A setting class marked `[Own]` (`permission`, `identity`) belongs to its actor's own row:

- It takes no run value: `Under()` takes none for it, the setting node's `Set` refuses it (403), and the `setting.save` and `setting.remove` actions refuse it (a step removing the identity row would lose the key). Its row is written only by its owner's code (`Permission.Add` after the prompt, the identity's code) through `actor.Setting.Save`.
- An `[Own]` row read without a signature is refused loudly (`SettingUnsigned`, 403); a row that can't be read names its key and says to delete it and set it again.
- An identity's private key is never carried by `get`, `list` or `create` (`Identity.Public()`), nor by `%MyIdentity%`. `export` is its only door, and it refuses (`ExportWaitsForCriticalActions`, 403) until critical actions exist, which ask more than a yes before a key leaves the app; nothing is asked and no key is handed out. Signing reads the full identity through the owner door `GetOrCreateDefaultAsync`.
- `create`, `rename`, `archive`, `unarchive` and `setDefault` ask through `Permission.Ask`; first-run and the owner's own code never ask.

What the permission prompts guard, and what they do not: `filesystem-permission.md`, "What the prompts guard".

---

## System.IO Is Banned in Production C# (use `path.@this`)

Action handlers and engine code under `PLang/app/**` must NOT call
`System.IO.*` directly. The only allowed filesystem surface is the
`app.type.path.@this` verb set (`Read`, `WriteText`,
`WriteBytes`, `Append`, `Mkdir`, `Delete`, `List`, `Stat`, `MoveTo`,
`CopyTo`, `ExistsAsync`, `AsBooleanAsync`). Every one of those methods
passes through `FilePath.AuthGate(verb)` before touching the disk.
`Read` lands a reference (`file`, `url`, `directory`); its content is the
reference's own value, and its raw bytes are `reference.Content` — both read
through the path's gated internal `Bytes`.

A handler reaching for `System.IO.File`, `System.IO.Directory`,
`System.IO.FileInfo`, or `System.IO.Path.*` (Combine/GetDirectoryName/
GetFullPath/...) is reaching **under** the auth gate. That means an
out-of-root path the actor never consented to gets read / written
silently. It's the filesystem analogue of the `Console.*` ban below.

**The rule.** A filesystem path in interior C# is `app.type.path.@this`
(the lowercase `path` alias). `string` only appears at the perimeter:
CLI args, JSON-on-disk shape (the wire format), and scheme-resolved DLL
loads. The app's anchors are paths, not strings: `app.filesystem.root` (the
app's folder, what `/` is) and `app.filesystem.os` (what ships with plang),
navigable as `%!app.filesystem.root%` and `%!app.filesystem.os%`.
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
`.Absolute`. The verb surface (`Read`, `WriteText`, `List`, `Stat`,
...) does this automatically — reach for verbs first;
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
