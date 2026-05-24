# security v1 — plan

## Branch
`compile-llm-notes-per-action` — per-action LLM teaching extracted from
C# `[Description]`/`[Example]` attributes into repo-tracked markdown
files under `os/system/modules/<module>/{module,<action>}.{description,notes,examples}.md`.
Compile prompt now reads from these files via a new loader.

## Scope of this audit
The branch delta past the runtime2 merge (`d8c18e257..55da8f529`).
Production C# net additions:

- `PLang/app/modules/MarkdownTeaching.cs` (new, ~140 LOC) — file-reading
  loader + orphan scanner.
- `PLang/app/modules/this.cs` (+63) — `MarkdownTeachingRoot`,
  `ResolveMarkdownTeachingRoot`, `WarnOrphansAsync`, and integration
  into `Describe()`.
- `PLang/app/goals/goal/steps/step/actions/action/this.cs` (+71) —
  `Notes`, `ModuleNotes`, `ExamplesMd`, `ModuleExamplesMd` record
  fields + three `*Rendered` accessors.
- ~130 action-handler files: **deletes** `[Description("...")]` /
  `[Example("...")]` attributes; content migrated to `.md` files
  checked into `os/system/modules/`.

No changes to: HTTP, auth/permission, signing, crypto, settings,
filesystem actions, IPC, shell-out, deserialization paths.

## Threat model
PLang is user-sovereign; this branch operates at **build time** to
assemble the system prompt sent to the planner LLM.

- **Inputs to the new loader:** `modulesRoot` (resolved from
  `App.OsDirectory`, a process-level path), `moduleName` (a C#
  namespace string from `_modules.Keys`), `actionName` (an action key
  from reflection over registered handler types). None of these
  originate from a PLang program or any network input.
- **Sink:** the LLM compile prompt (developer-facing build step).
- **Storage:** read-only `File.ReadAllText` over `.md` files in a
  repo-tracked directory tree.

## What to look for

1. **Path traversal in `MarkdownTeaching.Load` / `ScanOrphans`.** Could
   any caller pass a `moduleName` or `actionName` containing `..` or
   absolute path separators that would escape `modulesRoot`? Track
   where those strings come from — are they ever externally
   influenceable?
2. **Symlink/junction escape.** `Directory.EnumerateDirectories` +
   `Directory.EnumerateFiles` follow symlinks by default on .NET. Could
   a planted symlink inside `os/system/modules/` cause reads outside
   the intended tree? Trust level of that directory?
3. **Unbounded read.** `File.ReadAllText` with no size guard — does
   the new code introduce a sink where attacker-controlled file
   contents reach a security-sensitive consumer? (LLM-prompt content
   is excluded per ruleset.)
4. **Orphan-scan side channel.** `WarnOrphansAsync` writes paths to
   the actor `Output` channel. Could orphan file names leak sensitive
   filesystem layout to a remote consumer? Channel destination is the
   developer's terminal during `plang build`.
5. **Attribute deletion regressions.** ~130 attribute removals — does
   any of the removed attribute content carry security-relevant text
   that was previously surfaced in error messages or audit output and
   is now silently dropped? Spot-check `output/ask`, `output/write`,
   `signing/sign`, `signing/verify`, `settings/*`, `mock/action`.
6. **`MarkdownTeachingRoot` setter.** `public string?` setter on a
   process-singleton catalog — a PLang program cannot reach it (no
   reflection action exposed; not registered as a handler property),
   but confirm.

## Deliverables
- `.bot/compile-llm-notes-per-action/security/v1/plan.md`
- `.bot/compile-llm-notes-per-action/security/v1/result.md`
- `.bot/compile-llm-notes-per-action/security/v1/verdict.json`
- `.bot/compile-llm-notes-per-action/security/summary.md`
- `.bot/compile-llm-notes-per-action/security-report.json`
- Session appended to `.bot/compile-llm-notes-per-action/report.json`
