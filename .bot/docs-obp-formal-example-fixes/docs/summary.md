# docs — summary

**Version:** v1
**Branch:** `docs/obp-formal-example-fixes` (based on `app-systems`)

## What this is
The educator checked every example in `Documentation/v0.2/object_pattern_formal.md`
against app-systems code and found six that didn't match. This fixes the stale
examples so the doc states what the code IS.

## What was done
Verified all six against the code on this branch, then fixed five; the sixth was
branch drift (accurate here). No code changes — all were illustrative examples gone
stale.

| # | Was | Now |
|---|-----|-----|
| 1 | `app.Channel.Write(text)` (Law 1 + "Navigate, don't pass") | `app.Cache.Get`/`app.goal.current`/`app.module` — channels live on the actor, not the root |
| 2 | `callStack.Error.Add` / `.List` | `callStack.Audit.Add` / `.Newest` (real property is `Audit`; `error.list` has `Newest`, no `.List`) |
| 3 | `Lifecycle.Before.Run` / `Step.Load` | `on.start.Before` / `Step.Start` / `on.start.After` (`goal/this.cs`) |
| 4 | `app.type.type.@this`, `app/type/type/`, `App.Type` | `App.type` (lowercase); concrete type `app.type.item.text.@this` at `app/type/item/text/this.cs` |
| 5 | `A.Resize(Width, Height)` | real `file.save`: `Path.Use(async path => (data.@this) await path.Save(Value, Context))` |
| 6 | `path.Size` lazy | **unchanged** — `Size` is a lazy property on current app-systems; example is accurate (reporter checked an older commit) |

## Residual
Row 3's plang path `%!app.type.text%` kept as the educator wrote it (they corrected
only class/file). Worth a run-confirm — the educator has a running env.

## Learnings
`/learnings/docs-obp-formal-example-fixes/docs/v1/learnings.md`
