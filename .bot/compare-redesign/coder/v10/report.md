# v10 — clr dissolved from the wire: "a value renders itself" (+ error IS an item, TypedValueNode deleted)

Session goal (from Ingi): go into clr-dissolution, "show me the problem first,"
then kill clr. We did NOT touch `Judge` (Ingi: "we can't delete Judge yet").

## The arc, in one line
A value renders itself via `item.Write`; the writer is one dispatch, not a
type-switch. Three clr/sentinel sources that reached the wire as un-renderable
carriers are gone. clr-on-the-wire is eliminated across every suite.

## What landed (all committed + pushed on `compare-redesign`)

1. **`a252490` — value-renders-itself + 2 clr sources.**
   - Writer collapsed to `case item.@this v: v.Write(this)` (after dict/list);
     removed the `IsLeaf`-gate and the `TypedValueNode` arm *for items*.
   - **TypedValueNode rebox** (Wire cluster): Normalize tagged a registered
     *item* as a `TypedValueNode` (a non-item sentinel); re-boxing it into a
     dict-entry / nested Data re-ran `Lift`, which clr-wrapped it. Fix: a
     registered item passes through Normalize as itself.
   - **enum → clr**: `Lift` routed strings→text, ints→number via `OwnerOf`, but
     enums fell to the clr fallback. A CLR enum IS plang's `choice` → lift to
     `choice<T>` (renders its name). Cleared the permission-signing cluster
     (`Match` enum reached the wire as clr).
   - Gave the 5 renderer-only value types an instance `Write`
     (image/file/code/directory/url) + snapshot/hash, so every wire value
     renders itself. `image.Write` branches on `writer.Format`. `clr.Write`
     now names the carried CLR type (a clr at the wire = producer bug to surface).

2. **`737c5c4` — error IS an item.** (Ingi's call: don't make a NEW wrapper, use
   the error you already have.) `Error : global::app.type.item.@this, IError`
   (every concrete error derives from `Error`), tagged `[PlangType]` (parameterless
   — "error" derives from the class name; named form is reserved for non-derivable).
   When an error rides as a VALUE (`%!error%`, errors-trail, `throw %!error%`,
   snapshot) it renders/navigates itself: `Lift` returns it as-is (it's an item),
   `Error.Write` delegates to the error serializer, `Peek()` answers the `IError`
   so `%!error.Message%` and the `Clr<IError>()` reads in `throw` keep working.
   **`Data.Error` (the sidecar failure channel) is unrelated and unchanged.**
   The 2 producers that tagged an error as TypedValueNode now emit it directly.

3. **`a2ce15a` — TypedValueNode deleted.** No production code produced one after
   error became an item. Deleted the class, the writer arm, the Normalize else.
   Tests: deleted `TypedValueNodeNormalizeTests.cs` + the 2 TVN-dispatch methods
   in `IWriterFormatTests.cs`; the 5 serializer tests now hand the writer the
   value directly (it renders itself). Refreshed stale doc-comment crefs.

(Two more commits are bookkeeping: session report + the renderer-retirement plan
in `Documentation/Runtime2/todos.md`.)

## State — green-RELATIVE, zero regressions through the whole arc
clr carriers on the wire: **0** across all suites. Net failing counts moved
**down**: Types 26→13, Runtime 66→57, Wire 32→29; Modules ~106, Data ~99,
Generator 7 unchanged (their reds are NON-clr, predate this work). Every step was
verified by failing-SET diffs (not raw counts — the `plang --test`/suite binaries
segfault non-deterministically after printing, so trust set diffs + targeted
`--treenode-filter` runs, never raw counts).

## THE ONE PENDING DECISION (waiting on Ingi)
**Renderer-registry step 1** — I offered it and asked "now or checkpoint?".
`app/type/renderer/this.cs`'s write-dispatch (`Of`) is already dead (writer calls
`item.Write`). The only built-in consumer left is Normalize's
`types.Renderers.Has(typeName)` = the "this value renders itself, don't reflect
it" signal. Step 1 = move that signal onto the item:
- add `item.@this` virtual `RendersSelf => false`, override `=> true` on the
  non-leaf self-renderers: path (covers file/http/dir-path), file, url, code,
  directory, image, snapshot, hash, error. (Leaves already pass through via IsLeaf.)
- switch Normalize off `Renderers.Has` → `value is item it && it.RendersSelf`.
- drop the now-dead `renderers` ctor param + `_renderers` field from json/Writer
  (the only user — the TVN case — is gone). Update construction sites: `Wire.cs:519`
  + ~10 Wire test files that pass `renderers:`.
Wide-but-mechanical (ctor signature). I paused rather than start that churn unasked.

## Why renderer/this.cs can't just be deleted (Ingi asked)
It has a SECOND job: the **code-load DLL renderer seam**. `Loader.cs` instantiates
`ITypeRenderer` impls from `code.load`ed DLLs → `Renderers.Register(...)`, and
validates coverage via `Renderers.Has` (`Loader.cs:169`). So full deletion is a
Stage-7 concern: a runtime-loaded type should render via `item.Write` too, then
`ITypeRenderer`/`Register`/the Loader gate retire, THEN the file + catalog
`Renderers` property + the dead static `serializer/<fmt>.cs` Write methods all go
(KEEP their Read methods — the reader registry still uses them). Full plan in
`Documentation/Runtime2/todos.md` 2026-06-14 "retiring the renderer (write) registry".

## Where clr / the bigger picture stands
- **clr-on-the-wire: killed.** The clr CLASS still exists as the rung-2 carrier
  for genuinely-foreign CLR objects. Fully deleting the class = the big
  "force everything to a real item" migration (actions not `:item`, all domain
  types become items) — see the 2026-06-14 todos. Judge stays (Ingi's call).
- **Finishing the branch:** the branch still carries a large NON-clr standing red
  set (Modules ~106, Data ~99, Runtime ~57, …) that predates this arc and is
  unrelated to rendering. That's a separate effort — not started.

## Key facts for next context
- `item.@this.Write(IWriter)` is the one outbound render door (virtual; throws by
  default → "no bare wire form"). Leaves + path/file/url/code/directory/image/
  snapshot/hash/error override it. Format-specific render branches on `writer.Format`.
- `Error : item.@this, IError` — error is a value; `Data.Error` sidecar is separate.
- enum → `choice<T>` and IError → (itself, an item) happen in `Lift` (data/this.cs).
- LSP (csharp-ls) doesn't run the source generator → `Test`/`Assert`/`Context`/
  partial-property errors in the editor are NOISE. Trust `./dev.sh build`.
- Build/test: `./dev.sh build` / per-suite `PLang.Tests/<Suite>/bin/.../PLang.Tests.<Suite> --timeout 70s`. Warm at session start.
