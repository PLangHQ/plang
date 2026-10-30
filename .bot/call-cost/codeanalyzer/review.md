# Review — call-cost stage 1 (77add2895..4dcfa3f36)

Patterns first: 11 (a piece is made by its first asker), 12 (one answer), return early. Files read: `app/this.cs` (the cache), `call/this.cs` (whole from line 255 to the end, and the diff's regions above it), `goal/this.cs` (`Broken` and its caller), `BirthTests`.

## Bench (`Tools/Bench/run.sh`, birth + request, and `Bench repeat`/`phases`; bytes are load-independent; load 7–10)

| | Sub birth | Root birth | Sub request | Root request | Held | A goal run again (`repeat`, later runs) | Request born / load / run |
|---|---|---|---|---|---|---|---|
| 77add2895 (before) | 101 KB | 2260 | 441 | 3897 | flat | 227 KB | 98 / 43 / 299 |
| **4dcfa3f36 (stage 1)** | **54** | **2195** | **371** | **3832** | flat | **201** | **54 / 43 / 275** |
| change | −47 KB (−47%) | −65 | −70 (−16%) | −65 | — | −26 KB (−11%) | born −44, run −24 |

All inside or below their bands. The sub birth is the cache (idea 10: 47 KB, as measured); the run's −24 KB is the frame's `Parameter`/`Tags`/`Errors` (about 0.6 KB per frame over about 40 frames); `Broken` is not visible in the bytes (it is one small allocation per start). Times were measured at load 7–10 and are not compared. The coder's pins (sub birth 86,036 → 39,008 B, a frame with no write 2,592 → 1,544 B) agree with the rows.

## Findings (all low; none blocks)

1. **Pattern 12 — one lazy idiom written four times. Low.** `x ?? Interlocked.CompareExchange(ref x, new(...), null) ?? x` is spelled out for `Errors` (`call/this.cs:92`), `Tags` (`:156`), `Parameter` (`:312`), and `goal.Broken` has its own `Volatile` flag form (`goal/this.cs:396`). The question "made at its first ask, once, under a race" has four answers. One small owner (a `Lazy<T>` is the .NET one; a frame-lifetime object may justify the lighter CAS) answers it once and each site reads it.
2. **App cache — the seam is gone and its comment is stale. Low.** `app/this.cs:194–201`: `Cache` was `{ get; internal set; }` and the summary says "Swap via: - use 'redis.dll' for caching"; now it is `Lazy<ICache>` with a getter only. Nothing assigns `app.Cache` in the repo (the `llm.Cache` hits are an action's option), so no code broke, but the doc comment still promises a swap that no member offers. Either state the seam (a way to hand the lazy its provider before the first ask) or take the sentence out. Also: the `Lazy` makes the cache's life one object more per app (~56 B); fine.
3. **`Broken()` judged once assumes steps never change. Low, a thing to confirm.** `goal/this.cs:396`: "a loaded goal's steps don't change", but a step's `Error` is `internal set` and the builder writes it (`goal/step/list/this.cs:295`, after the retry). A goal built and then started in the same process (the test runner's build-then-run) would keep the answer it was first asked. The producer-side fix is that writing a step's `Error` clears its goal's judgment; or a test that builds a goal that was already started and expects `GoalBroken`.
4. **Pattern 11 — the frame still makes two collections at birth. Note.** `call/this.cs:251–252`: `property = new(stack.Properties)` and `Children = new child.list.@this(stack)` are still made per frame; `Children.Add(frame)` is called on every push (`call/list/this.cs:234`), so it is not lazy by an easy edit, but `Children.Remove(this)` at dispose (`:469`) shows a history setting that can make most frames never need it. Stage 2 or 3 territory.
5. **Return early — `Begin`. Low.** `call/this.cs:262–278`: the method ends in two sibling `if`s that each wrap their body (`if (setting.Timing.Value) {…}`, `if (… && _store != null) {…}`); it reads correctly, no wrapped-rest nesting. Nothing to change; named because the rule asked every touched file be looked at. The other touched regions (`Record`, `Tag`, `TryGet`, `Names`) guard first.

## Closed / good

- `Error` reads `_errors?.Newest` without making the list; `Record`, `TryGet`, `Names`, `Holds` read the field and never make a piece they only look at — the readers are the right askers (pattern 11).
- `Born`, `Set` and the setter go through the property, so a write makes it (and the setter registers `_arguments`).
- The CAS idiom is correct: `CompareExchange` returns the old (null on a win), then `?? _errors` reads the winner.
- The pins (`BirthTests`) state the bytes each lazy piece saves.

## Verdict v1: **PASS** — tester next. Stage 2 is measured on its own hash.
