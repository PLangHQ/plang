# v3 — "do everything": the remaining core concept pages

## Context

Ingi: "do everything." v1 did app + module; v2 did type + the value lattice.
v3 writes the `start.md` for the remaining **core** concepts a C# developer must
understand to read the runtime — the ones execution flows through and the ones
every handler touches.

Same contract: in-tree `start.md` next to each `this.cs`; C#-developer audience;
*what → where → plang mapping*; point to the teaching layer, don't duplicate;
every precise claim verified against source before commit.

## Wave A (this version) — 7 pages

| Page | Concept | Spine source |
|---|---|---|
| `PLang/app/goal/start.md` | goals + steps + step→action dispatch | goal/this.cs, goal/step/this.cs, goal/list/this.cs, goal/step/action/this.cs |
| `PLang/app/data/start.md` | the universal carrier (Data, Data<T>, Ok/Fail/Merge, wire) | data/this.cs, data/Properties.cs, data/Wire.cs, data/this.Result.cs |
| `PLang/app/channel/start.md` | redirectable I/O (stream/goal/session/…) | channel/this.cs, channel/list/this.cs, channel/type/stream/this.cs |
| `PLang/app/event/start.md` | the event door, core events, binding, clauses | event/this.cs, event/on/this.cs, event/on/start.cs, event/binding/this.cs |
| `PLang/app/actor/start.md` | execution identity + the context | actor/this.cs, actor/list/this.cs, actor/context/this.cs |
| `PLang/app/call/start.md` | the callstack, frames, variable memory | call/this.cs, call/list/this.cs, call/setting/this.cs |
| `PLang/app/error/start.md` | Error, the subclasses, answer-vs-throw | error/Error.cs, error/list/this.cs, the *Error.cs subclasses |

## Method

Each page is drafted by a **Haiku (haiku-5.5) subagent** pointed at: the shared
style brief (`scratchpad/style-brief.md`), its spine source files (to READ and
verify, not guess), a short section skeleton, and the v1/v2 pages as the voice
reference. Each reports what it could not verify. I then **verify the risky
claims against source** (locked-core surfaces especially: event, call, data) and
commit.

The locked core (`data`, `channel`, events, call/variable memory) is documented
as it IS — these pages describe current surface, they do not propose changes.

## Wave B (next version, if budget) — the smaller concepts

`setting`, `store`, `snapshot`, `task`, `test`, `keepalive`, `service`,
`warning`, `parent`, `shortcut`, and `variable` (under type/item/variable). These
are smaller; lighter pages.

## Verification focus

- `data`: the `{name, type, value, properties, signature}` shape; Ok/Fail/
  FromError/Merge; `[Out]`/`[Store]`/`[Debug]`; the courier-vs-leaf rule.
- `event`: the real core-event list (from `event/on/*.cs` filenames) — no invented
  events; Before/After.
- `call`: `context.Variable` is the memory; frame-scoped format/settings.
- `goal`: the `.pr` step → `module`+`name` → handler path (already shown on the
  module page — cross-link, don't re-derive).
- `error`: the real subclass list (from `error/*Error.cs`); answer-vs-throw rule.

## Not in scope

No production `.cs` edits (except possibly fixing the already-flagged stale
`this[System.Type]` XML comment if I touch that area — otherwise it stays a
finding). No `.goal` test examples. Stale `Documentation/*.md` untouched.
