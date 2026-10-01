# v1 — Fix stale examples in object_pattern_formal.md

## Branch
`docs/obp-formal-example-fixes`, based on `app-systems`.

## Source
Educator verified every example in `Documentation/v0.2/object_pattern_formal.md`
against app-systems code and reported six mismatches. Each verified against the
code on this branch before editing (docs state what the code IS).

## Decisions (fix in doc, not code — all are illustrative examples gone stale)
1. Law 1 `app.Channel.Write` → root-navigation using real `app` members (channels
   live on the actor, not the root). Also fixes the duplicate in "Navigate, don't pass".
2. "Collection is the API" `callStack.Error` → `callStack.Audit` (+ `.Newest`, not `.List`).
3. "Owner does the work" `Lifecycle.Before.Run` / `Step.Load` → `on.start.Before` /
   `Step.Start` / `on.start.After`.
4. Three-paths table: drop the non-existent `app.type.type.@this` / `app/type/type/`;
   fix `App.Type` → `App.type`; concrete type → `app.type.item.text.@this`.
5. "Data rides sealed" `A.Resize(...)` → the real `file.save` `Path.Use(... path.Save(Value, Context))`.
6. `path.Size` → `path.FileName` (genuinely lazy-cached), with a note that I/O
   knowledge uses `path.Size(context)` through the gate. (Initial pass wrongly left
   this because the branch base was 76 commits behind origin/app-systems; after
   merging current app-systems, `Size` is the async-gated method and the old
   property example was indeed stale.)

## Confirmed
Row 3's plang path `%!app.type.text%` resolves (educator ran it; lands on
`app.type.item.text`).

## Status
Doc fixes applied; no code changes warranted. Learnings in
`/learnings/docs-obp-formal-example-fixes/docs/v1/learnings.md`.
