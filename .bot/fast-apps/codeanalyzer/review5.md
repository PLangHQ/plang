# fast-apps: fifth review — review4's fixes (decision 86, tip d050b9f50) and the verdict for the branch

Scope: the nine production files the coder changed since review4 (02aff152e..d050b9f50, +116/−84; one test line), read from the diffs and, where the shape needed it, the whole file: `type/list/Registry.cs`, `origin/code.cs`, `goal/setup/this.cs`, `goal/list/this.cs`, `call/list/this.cs`, `module/code/this.cs` and `this.Snapshot.cs`, `test/this.cs`, `test/report/this.cs`, `PLang.csproj`. Review4 carried every other touched file whole (none has a changed hunk). Read only; I did not run tests or `Tools/ObpScan`. I re-grepped the callers of `Add(kind)`.

**Verdict: NEEDS WORK (FAIL, coder next) — one small blocking finding, otherwise the branch passes.** Every review4 finding is fixed at the root, and the root birth is back inside its band. The one blocker is the rule-35 shape coming back one level up: the fix for N1 moved the origin's only call onto a new internal overload, so the public `Add(kind)` is now called by tests alone.

## review4's findings, one by one

| review4 | Verdict | Where |
|---|---|---|
| N1 `Add(kind)` holds then guards, a full scan per add | Fixed at the root | `Registry.cs` `Add(kind) => Add([kind])` and `internal Add(IReadOnlyList<kind>)`: `Guard(kinds)` against the held formats and against the others *before* any is held, then `Hold` each. `Formats()` (the held formats by key) is built only when an incoming kind answers to a MIME or extension (`seen ??= Formats()`), so the three path schemes (no key) pay no scan. `origin/code.cs:18` adds the three as one list. **Measured:** root birth 2,359–2,360 KB, inside the 2,357–2,377 band (was 2,488) |
| N2 setup adds every `.pr` it reads | Fixed, with a note (finding 3) | `goal/setup/this.cs`: `if (await read.Value() is goal.@this { IsSetup: false } other) await _goals.Remove(other, context);` |
| `QRCoder-ImageSharp` hidden dependency | Fixed | `PLang.csproj`: `SixLabors.ImageSharp` 2.0.0 referenced directly (finding 6 on its version) |
| N3 `Failure` → verb | Fixed | `test.Explain(sb, context)` (`test/this.cs`, `test/report/this.cs`) |
| N4 `Built` | Fixed | `Unbuilt` (`goal/list/this.cs:57`), a participle-free noun for the refusal; the `??` chain protocol (null = not mine) remains |
| N5 `stat` copies the pr | Fixed | `goal/list/this.cs:123` `goal.PrPath is { } held ? new stamp.stat(held, info) : new stamp.unknown()`: the stamp takes the goal's own `PrPath`; the local is named `held` (finding 5) |
| N6 snapshot writes lists twice | Fixed | `module/code/this.Snapshot.cs`: one tail, the loop guarded by `if (table != null)` (readability, finding 5) |
| N7 names | Fixed | `Deepen()` → `IsFull` (a predicate; the throw is repeated, finding 4); `_rows`/`Rows` → `_table`/`Table` over the class `table` (`Put(table table, …)`, the type-and-name idiom, fine) |
| N8 `none` per goal | Not taken | not in the coder's list; trivial |
| Index key invariant | Ruled (82/86) | stays stated |

## Blocking

### 1. Production code only tests call (rule 35), again. `type/list/Registry.cs` public `Add(kind)`
The fix for N1 put the origin on the new `internal Add(IReadOnlyList<kind>)` (`origin/code.cs:18`). A grep of `PLang` finds no other caller of `type.Add(…)`; the public `Add(global::app.type.kind.@this kind) => Add([kind])` is called by tests alone (`PLang.Tests`: `FormatKindTests`, `SharedTypeListTests`, `ChoiceSetNameTests`, `RuntimeTypeLoadingTests`, `SharedGoalTests`). In review3 I passed it because it had a production caller; it no longer has one. Ingi's rule (35): code only tests call goes, with its tests, or tests use what production uses.
- **Fix shape:** make the single-kind `Add(kind)` `internal` (the tests see internals) so no public member exists for tests alone, and re-pin the surface golden with the decision that approved it — a narrowing of `type.list`'s public surface, so the architect brings it to Ingi. If he keeps it public as a door for outside callers, say so at the member; then this is noted, not blocking.
- **Blocking:** yes by 35, small; one line plus a re-pin.

## Not blocking

2. **The collision rule is now written three times.** `Guard()` (every held format against each other, `Registry.cs:292`), `Guard(kinds)` (incoming against held and against each other), `Formats()` (every held format by key), each a `foreach type → root.Kinds → Mime.Concat(Extension)` loop, plus `Collision(key, taken, kind)` they share. `Guard()` is `Guard(all held kinds)` against nothing. Fix: one walk that yields the held formats, one `Guard(kinds)` that checks a list against it (`Guard()` passes the held ones).
3. **A refused later kind leaves the earlier ones held, and setup adds then removes.** `Add(IReadOnlyList)`: `Hold(kind) is { } unheld` can still refuse a kind after earlier ones in the list were held, so "a refused one leaves nothing held" holds for the guard but not for `Hold`. In `goal/setup/this.cs` a non-setup goal at a setup path is read (so `on.load` fires, it is stamped and added) and then removed: the effect is right and the work is wasted, and a failing `Remove` is unchecked. Fix: read, test `IsSetup`, and add only setup goals through the list's reading door taking the keep decision; and hold the batch's kinds in one step or undo the held ones.
4. **The overflow throw is repeated in three `Push` overloads.** `call/list/this.cs:185,204,212` `if (IsFull) throw new CallStackOverflowException(MaxDepth);` — a fourth deepening overload can still forget it (the review4 N7 point, renamed). Fix: one `Refuse()`-style member each overload calls first, or the check in the one place every deepening frame passes.
5. **Small.** `goal/list/this.cs:123` the local `held` is a role word (`pr` is what it holds: `goal.PrPath is { } pr`); `Snapshot.cs` `if (table != null)` followed by an unbraced `foreach` at the same indent reads as two statements; `goal.Stamp` still allocates a `none` per goal.
6. **`SixLabors.ImageSharp` 2.0.0 is whatever `QRCoder-ImageSharp` pulled in.** Check the version against published advisories before the line moves to security; I did not.

## Bench (`Tools/Bench/run.sh`, birth and build; tip d050b9f50)

| Number | Stage 5 | review4 row | Now (review5-d050b9f50) | Band | Reading |
|---|---|---|---|---|---|
| Root birth, KB | 2,357–2,377 | 2,488 | **2,360** | ±25 | **back in band** (N1 fixed; the coder's own row: 2,359 at load 2.6) |
| Sub birth, KB | 122–128 | 110 | 110 | ±5 | lower, stable |
| Root / sub birth, ms | 18.4–20.5 / 0.15 | 18.5 / 0.14 | 29.8 / 0.27 | ±1.5 / ±0.02 | **load-inflated** (this row's load 12.7); the coder's row at load 2.6: 18.62 / 0.15, in band |
| Build, clean | quiet band 48.7–53.5 s | 90.5, rerun 74.9 | 98.3 s | ±3 s quiet | **still not settled**: load 12.7 |
| Build, incremental | 11.5 s | 79.4, rerun 9.1 | 15.6 s | ±1.2 s | at load 12.7; the quiet rerun said 9.1 |

The machine did not stay quiet through this run either (the row's load 12.7), so the times are not usable; bytes are, and they are in band. Held memory, the requests, `plang --test` and the sweep were not re-run (nothing in this round touches them; review4's row stands). **The clean-build row still needs a quiet machine** — the tester's, per the architect.

## Checked, clean (explicit no)
- No static added; no `caller == null` or `_parent` null; no flag read at many doors; no lock held across an await in the changed hunks.
- Locked core: the new `internal Add(IReadOnlyList<kind>)`, `Guard(kinds)`, `Formats()` are internal/private; `test.Explain` renames a `test` member (not on the locked list); nothing public added to `app`, `item`, `type`, `type.list`, `kind`, `property`, `reader`, `IWriter`, `data`, `call` or `event`. The public `Add(kind)` was already there (finding 1).
- `IsFull` reads the same `_current.Value` the old `Deepen()` read, before any frame exists; `binding` and `Isolate` stay unchecked; no diff scope leaks.
- No history or task comments added; `Table`/`_table`/`table` is consistent across `module/code`; `Explain` and `Unbuilt` name what they are.

## For the coder
1. Make the single-kind `Add(kind)` internal (or give it a production caller on purpose), re-pinned with the decision (finding 1).
2. Findings 2–5 as cheap cleanups; 6 before security.
