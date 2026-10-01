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
6. `path.Size`: NOT changed — it is a lazy property on current app-systems, so the
   example is accurate. Was branch drift from the reporter's commit.

## Residual to confirm (educator has a running env)
Row 3's plang path `%!app.type.text%` was kept as the educator wrote it (they only
corrected the class/file). Worth a run-confirm that it resolves.

## Status
Doc fixes applied; no code changes warranted. Learnings in
`/learnings/docs-obp-formal-example-fixes/docs/v1/learnings.md`.
