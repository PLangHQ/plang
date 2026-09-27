## 2026-09-27 night — Ingi asleep; the architect decides (his words: "I want you to make decisions")

Coder works stage by stage; every decision made without Ingi is logged here, with its reason and where it's written, so he can overturn any of them in the morning.

**Decisions made while Ingi slept:**
| # | Stage | Decision | Why | Where |
|---|-------|----------|-----|-------|
| 1 | 3 | `ClrType` has one meaning: the item class (text → `text.@this`). The C# value underneath (datetime → `DateTimeOffset`, `{number, int}` → `Int32`) belongs to its owner: a number kind's storage, a scalar item's `OwnedClrTypes`; `variable.set` asks the kind or item | one name had two meanings (nothing named two ways) | plan.md stage 3 |
| 2 | 3 | An item's own type isn't born through the type list (items hold no context): the ~35 `item.Type` overrides pass their own class (`typeof(@this)`); everything holding a context is born through the list | narrows ruling (a) but keeps its point: every type knows its class, the self-lookups go | plan.md stage 3 |

## 2026-09-27 — app-systems plan ready for coder

Eight review rounds with Ingi on `plan.md` (2026-09-24, -26, -27); round 8 passed without comment and Ingi called it ready. Every `app.X` is the type X: one generic `type<X>` over its concept's `list<X>`, with `Get(key)` as the one async door, `Match` on the element, `current` via the element's `static virtual`, and the list as the one store. Round 6–7 added: settings as C# classes per owner (one row per actor per class, user falls back to system, `app.store` for owned data), test split into settings / list / a session channel / report, one event class per verb (`app/event/on/create.cs` with `before`/`after`; the running event is `this`), sub-goal addresses `/start#show`, `item.history`, one kind member, and the deletion of the v0.1 `.pr` files in stage 0. Parked follow-ups in `Documentation/Runtime2/todos.md`: the `pr` object, variable storage by identity (isolated data pattern), a `nothing` type. Sent to coder 2026-09-27; coder reviews plan.md first, then stage 0.

Stage status:
| Stage | What | Status |
|-------|------|--------|
| 0 | Base: re-record Compile, baseline, delete v0.1 .pr, .dll, .pdb files | complete (57d180afe, 9ee92ac9c, c918a9a1b) |
| 1 | `Run` → `Start` (+ test/environment/callback.start, range From/To, test stopwatch Begin) | complete (4b396e780) |
| 2 | folded into 4 | — |
| 3 | One set of types | in progress |
| 4 | The collected type | pending |
| 5 | Faces and honest facts | pending |
| 6 | The reference (variable parser) | pending |
| 7 | Every concept is its type (+ settings, test) | pending |
| 8 | `on` on every object | pending |
| 9 | Module pass | pending |
| 10 | `%!app` holds its types | pending |
| 11 | Tests through the app's doors | pending |
| 12 | Exception pass | pending |
