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
| 6 | `path.Size` lazy property | `path.FileName` (genuinely lazy-cached, `_fileName ??= …`), plus a note that I/O knowledge goes through the gate via `path.Size(context)` |

## Base correction
My first pass branched off a **76-commits-behind** `app-systems` where `Size` was a
plain property — so I initially left #6, wrongly. The educator confirmed current
`app-systems` (3a709545a) has `Size` as an async method taking context ("through the
gate"), so the doc's lazy-property `path.Size` *was* out of date. Merged current
`app-systems` in and fixed #6. All six now resolved.

## Confirmed
Row 3's plang path `%!app.type.text%` resolves (educator ran it; lands on
`app.type.item.text`) — matches the corrected class/file.

## Learnings
`/learnings/docs-obp-formal-example-fixes/docs/v1/learnings.md`
