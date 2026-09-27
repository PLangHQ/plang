## 2026-09-27 night — Ingi asleep; the architect decides (his words: "I want you to make decisions")

Coder works stage by stage; every decision made without Ingi is logged here, with its reason and where it's written, so he can overturn any of them in the morning.

**Decisions made while Ingi slept:**
| # | Stage | Decision | Why | Where |
|---|-------|----------|-----|-------|
| 1 | 3 | `ClrType` has one meaning: the item class (text → `text.@this`). The C# value underneath (datetime → `DateTimeOffset`, `{number, int}` → `Int32`) belongs to its owner: a number kind's storage, a scalar item's `OwnedClrTypes`; `variable.set` asks the kind or item | one name had two meanings (nothing named two ways) | plan.md stage 3 |
| 2 | 3 | An item's own type isn't born through the type list (items hold no context): the ~35 `item.Type` overrides pass their own class (`typeof(@this)`); everything holding a context is born through the list | narrows ruling (a) but keeps its point: every type knows its class, the self-lookups go | plan.md stage 3 |
| 4 | 3 | Kinds join one store and own their type: number's 15 precisions derive from the kind base, hash's algorithms become kind classes (`crypto/type/hash/kind/`), each kind class declares its type, owns its aliases; `kind.list(context)` answers full types; choice sets and path schemes become their types' kinds; `kind` never null, renamed lowercase so every old null check breaks at compile; the empty kind is never written to the wire | the coder's three-commit proposal matches the plan and Ingi's "type is a property on the class" | coder's stage 3 |
| 5 | 3→4 | The global kind store stays at `app.type.kind` through stage 3 and moves in stage 4 to item's kind list | once `app.type` is `type<type>`, `app.type.kind` means the type `type`'s own kinds | plan stage 4 (as planned) |
| 6 | 3 | Rebuild the stale fixture DLLs (TypeProvider, SignatureRendererShadow) as the close of stage 3 | they test `code.load` → `Add`, which stage 3 rewrote; red since before the branch | coder's stage 3 |
| 7 | 3 | `set.@this`'s two private statics (the set's name for the base ctor) are accepted where C# forces them; preferred shape: the base takes the enum's type and names the set itself | no statics in OBP, but a base-ctor argument before the instance exists is C#'s constraint | coder's call |
| 8 | 3 | Fixture DLLs: namespace fix, `TypeProvider.Money` becomes an item (and the in-test fixtures), `CustomInt` and its renderer deleted | only items are plang types since the one set; int is a kind (decision 3) | coder's stage 3 close |
| 9 | 4 | `App.Type` sites: by-class, identity and built-in-name lookups move to `context.App.type.list[…]` (sync, today's throw kept until stage 12); only names the programmer or `.pr` wrote move to `await app.type.Get(…)` | 70 production + 217 test sites; the result door is for names that can miss | plan stage 4 |
| 10 | 4 | The base list's `Add` becomes virtual and the other adders (`Add(list)`, `Add(Data)`, `Insert`, `Put`) route through it; the registry overrides it with its checks | `new Add` would hide only through the registry's own type, and a caller holding a plain list would bypass the sealed and one-name checks | plan stage 4 |
| 11 | 4 | Stage 4's `Get(key)` walks the list; it switches to `all()` in stage 7 | `all(setting)` arrives in stage 7 | plan stage 4 |
| 12 | 4 | Kinds live on their types; the type list answers kind lookups by name and by class by walking its types' kinds; no flat kind store beside them. Supersedes "the global store becomes item's kind list" in letter (number's, hash's, sets' and schemes' kinds need their own homes), keeps it in shape | one store (Ingi: "list.where(prpath == …)"); the global store answered for every type's kinds, not item's alone | plan stage 4 |
| 13 | 4 | Replaces 10: instead of a virtual `Add` that every adder routes through, one protected guard every slot write passes (`Admit(slot)`, `Admit(list)` for a chunk), a no-op on the base list, overridden by the registry (a violation throws: only plang's own code can reach the slots) | routing through a per-item `Add` makes every list's extend O(n) (chunks are O(1) by design) and opens lazy rows; the guard keeps the guarantee without the cost | coder's stage 4 |
| 14 | 4 | The identity door becomes `app.type.list[type, context]` and `Mime(mime, context)`, reading `context.App.Format` | Ingi: "we use context, context.app.format" | coder's stage 4 |
| 15 | 4 | `type<T>` takes its list class as a second parameter: `type.@this<T, L> where L : list<T>`, the element naming it through `IList<TSelf, L>`; app declares `type.@this<type.@this, type.list.@this> type` (later `…<goal.@this, goal.list.@this> goal`) | `app.type.list.Mime(…)` and goal's loading list need the list's own class without casts; a typed accessor beside `list` would hold the list twice | coder's stage 4 |
| 16 | 4→7 | The type list's `type` entry is `app.type` itself (replaced at app construction, same class); for the other concept types the same move, with the type reading its facts itself instead of copies on entries, is settled in stage 7 with goal in hand | today `%!app.type.type%` answers a fact-less entry, a different object from `%!app.type%` | plan stages 4, 7 |
| 17 | 4→7 | The prompt's `%!app.X["key"]%` line moves from stage 4 to stage 7 | no builder prompt or goal reads `%!app…%` yet; stage 4 stays builder-neutral (twins byte-equal), stage 7 is where goal/module/actor become reachable | plan stages 4, 7 |
| 18 | 5 | A type's face is written only in the Out view; every other view (Store included) writes its identity, and the Data's `type` slot never changes | a face in the Store view would change every `.pr` holding a type | coder's stage 5 |
| 19 | 5 | The honest facts (real descriptions/examples) go first, with the one eval run spent on them; internal items (`wire`, `clr`) declare `Internal` and stay in the list but out of the face and prompt C | prompt C prints each property type's facts, so this is the builder-visible commit | coder's stage 5 |
| 20 | 5 | The 357 goal files: the description moves above the name (Edit tool, a folder at a time), then `plang build` re-saves the os/ `.pr` with every step cached; any `.pr` change beyond comments and hashes stops the step | the hash covers comments now; a step that re-asks the LLM must not rewrite its formal unreviewed | coder's stage 5 |
| 21 | 5 | The honest-facts eval is accepted as is (golden 58/58; the builder's 12: 9 first try, 2 after retry, 1 refused on a dict-literal concatenation, vs 2 refused before); goal's description, added when `goal.Description` goes, is checked by twin equality only, no second eval | one eval run per builder-visible change, no nano chasing | coder's stage 5 (5d4311778) |
| 3 | 3 | Test `LoadDll_CustomInt_OverridesBuiltInName` (a DLL overriding "int") is deleted; what it guarded flips: a loaded DLL claiming a taken type name is refused (`Add` answers an error) | contradicts one name one type and "int is a kind"; already failing in the baseline | coder's stage 3 |

## 2026-09-27 — app-systems plan ready for coder

Eight review rounds with Ingi on `plan.md` (2026-09-24, -26, -27); round 8 passed without comment and Ingi called it ready. Every `app.X` is the type X: one generic `type<X>` over its concept's `list<X>`, with `Get(key)` as the one async door, `Match` on the element, `current` via the element's `static virtual`, and the list as the one store. Round 6–7 added: settings as C# classes per owner (one row per actor per class, user falls back to system, `app.store` for owned data), test split into settings / list / a session channel / report, one event class per verb (`app/event/on/create.cs` with `before`/`after`; the running event is `this`), sub-goal addresses `/start#show`, `item.history`, one kind member, and the deletion of the v0.1 `.pr` files in stage 0. Parked follow-ups in `Documentation/Runtime2/todos.md`: the `pr` object, variable storage by identity (isolated data pattern), a `nothing` type. Sent to coder 2026-09-27; coder reviews plan.md first, then stage 0.

Stage status:
| Stage | What | Status |
|-------|------|--------|
| 0 | Base: re-record Compile, baseline, delete v0.1 .pr, .dll, .pdb files | complete (57d180afe, 9ee92ac9c, c918a9a1b) |
| 1 | `Run` → `Start` (+ test/environment/callback.start, range From/To, test stopwatch Begin) | complete (4b396e780) |
| 2 | folded into 4 | — |
| 3 | One set of types | complete (4bfd09c61 … f88e1f8d3; suites at or under baseline: Types 23 → 19, Data 45 → 44; twins byte-equal) |
| 4 | The collected type | complete (010bfa885 … a6d4e740b + the `type` entry; timing unchanged, no index; builder-neutral) |
| 5 | Faces and honest facts | in progress |
| 6 | The reference (variable parser) | pending |
| 7 | Every concept is its type (+ settings, test) | pending |
| 8 | `on` on every object | pending |
| 9 | Module pass | pending |
| 10 | `%!app` holds its types | pending |
| 11 | Tests through the app's doors | pending |
| 12 | Exception pass | pending |
