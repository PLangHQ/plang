# coder v7 — stage 7 plan: every concept is its type

Branch `app-systems`. Plan: architect `plan.md` stage 7 row, "One list, one type" (`all`), and
"Settings (stage 7)". Slices from plang-40, each green against the baseline and pushed on its own:
7a goal → 7b module + actor → 7c test → 7d variable → 7e settings → 7f concept types in the type list + the
prompt line (twins + one eval). Trace first per slice; this file opens with 7a.

## 7a trace — goal

**Today** `app.Goal` is `goal.list.@this` (`app/this.cs:145`), a plain class over three dictionaries
(`_goals` by PrPath, `_byPath` by Path, `_byName` fuzzy by name) with `Get(string)` scanning forms (name,
`.goal`, `/leaf`, PrPath, slash-qualified). Production callers of the collection are few:

| caller | uses |
|---|---|
| `module/action/goal/call.cs:80,87` | `GetAsync(name, caller)` — call's bare-name lookup (caller chain → cache → load `.pr` near the caller, then root/system) |
| `ui/code/Fluid.cs:390` | `GetAsync(goalName)` (no caller) |
| `module/action/build/this.cs:112`, `test/start.cs:206` | `Load(pr)` |
| `callstack/this.Snapshot.cs:212-213` | `Get(goalName) ?? Get(goalPrPath)` (snapshot restore; its structure is parked, #18) |
| `goal/setup/this.cs:25,61` | `AllIncludingSetup`, `Add` |

Tests: `Add` ×104, `Setup` ×28, `Load` ×11, `GetAsync` ×9, `this[…]` ×7, `Get` ×6, `list` ×2, `current` ×1.

goal is already an item with `ICreate` (`goal/this.Item.cs`); it has `Address` (`goal/this.cs:216`, the
.goal path without extension) but no `Match`/`Current`/`List`. A sub-goal's `Parent` is set by the `.pr`
reader (`goal/serializer/Reader.cs:44`) but not by `goal.Parse` (`goal/this.cs:632`, `Child.Add` on a
naked `List<goal>`); a sub-goal's `Address` is its file's, same as its parent's.

**Shape (as the plan says):**
```
app.goal                  type<goal, goal.list>        (app/this.cs: `goal`, lowercase)
app.goal.list             goal.list : list<goal>       rows are the loaded goals; Add replaces the same PrPath
app.goal.list.all(setting) goal's override: the .pr files under .build/ of the app and of /system/,
                          app's copy winning; setting { os = true, visibility = [public] }
app.goal.Get("/show")     walks all(): goal.Match(key) — its Address, or a child's `/start#show`
app.goal.current          goal.Current(context) — the running goal
list navigation `.all`    base list word, before the empty check (type/item/list/this.cs Get)
```
Goes: `_goals`/`_byPath`/`_byName`, `Get(string)`'s form scans, `this[string]`, `this[path]`, `list`
(the property), `Names`, `All`, `int Count`, `Public`, `Events`, `Contains(string)`, `Remove(string)`,
`Clear`, the stale "no app-level current" comment. Stays on the list as its work: `Load(pr)`, `Setup`,
call's lookup.

## 7a — as built (commit da26b54dd; its message wrongly repeats "stage 7 plan, 7a trace" — the scratch
message file didn't update; not rewritten, it's pushed)

All eight answers taken as ruled; call's lookup is `goal.list.Find(name, caller)`. The walk under
`all(setting)` is `list<T>.Every(setting)` (internal, lazy); `type<T, L>.Get(key)` walks it, so the goal type
stops reading at its match. Every yields the goals already held first (no reading), then each listed
`.pr` not held. `goal.list.setting.Of(goal)` answers which of a file's goals a setting lists.
Tests: GoalsTests and GoalAccessorTests rewritten for the new doors; sync `Get(name)` sites use
`await …Find(name)`; tests asking "is it held yet" check `goal.list.Items()`. A test binary has no `os/`
beside it, so the system listing is empty in tests.

## 7a — what doesn't hold as written, with proposals

1. **"Lazy" `all()`.** The default is "the public goals, one per `.pr`, from the listing alone", but a
   `list<goal>` holds goals, and a goal's facts come from its `.pr`. Proposal: `all()` lists the `.pr`
   files once and reads each (through the list's own `Load`, so a goal read once is the same instance);
   the answer is cached on the list until a goal is added. A `.pr` that doesn't read (an old format) is
   left out and named in a warning, not fatal. The alternative — a lazy goal shell that reads its `.pr`
   on first touch — means every goal member guards a load; I'd avoid it.
2. **Visibility "derived from the parent".** Visibility is stored (`[Store]`, written and read in the
   `.pr`). Proposal: `Visibility => Parent == null ? Public : Private`; the `.pr` still writes it (same
   bytes), the reader stops reading it. The build's `Parse` already assigns exactly this.
3. **Parent set where the child is added.** `Child` is a naked `List<goal>`. Proposal: `Child` becomes a
   small `list<goal>` of the goal's own (`goal/child/this.cs`) whose `Admit` sets the child's `Parent` —
   then `Parse`, the reader and anyone else adding a sub-goal get it for free. (Admit is today a guard;
   this makes it the one place a slot is taken, which is what it is.)
4. **Sub-goal address.** `Address => Parent is { } p ? $"{p.Address}#{Name}" : path-derived`. Callers
   that read `Address` today for a sub-goal (pick's `call` check, `Get`'s slash-qualified scan) —
   traced, both only ever see top goals or compare names.
5. **Snapshot restore** (`Get(goalName) ?? Get(goalPrPath)`, parked structure): keep its behaviour by
   walking `app.goal.list` for the goal with that PrPath (then name) — no new name lookup on the list.
6. **Call's lookup** (`GetAsync`): it is the list's loading work (caller chain, near folders, root,
   `/system`), used by `goal.call` and Fluid. Proposal: it stays on `goal.list`, named for what it
   answers — `Callee(name, caller)` ("the goal a call names"), beside `Load`. (`goal.Callee(context)`
   already exists on goal for "every goal this goal reaches"; a different question on a different
   owner, same word — or `Called`, if you'd rather they differ.)
7. **Setting class before 7e.** `all(setting)` needs `goal/list/setting/this.cs` now; 7a adds the plain
   class (`Os = true`, `Visibility = [Public]`) and the dict → class step; `ISetting<T>` and the builder
   reading it land in 7e.
8. **Adding keeps one goal per PrPath** (today's dictionary semantics, which tests rely on — `Add` ×104):
   goal.list overrides nothing on `Add`; it removes the goal already at that PrPath first (its own
   `Add(goal)`, the typed door beside the base's untyped ones).
