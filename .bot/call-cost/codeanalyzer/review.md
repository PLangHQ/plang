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

---

# Review v2 — call-cost stage 2 (6f6b46e29 code = 4dcfa3f36 .. 1878bef03)

Patterns first: 5/7 (a type is found, not made), 11 (lazy), 12 (one answer), return early, and the new tell (a value answers equality). Diff read (13 files); the touched methods read whole; the remaining `new type(...)` sites listed from `git grep` (below).

## Bench (`Tools/Bench/run.sh` birth + request, `Bench repeat`/`phases`; bytes load-independent, load 7–18)

| | Sub birth | Root birth | Sub request | Root request | Held | A goal run again (`repeat`) | Request born / load / run |
|---|---|---|---|---|---|---|---|
| 77add2895 (before stage 1) | 101 KB | 2260 | 441 | 3897 | flat | 227 KB | 98 / 43 / 299 |
| 4dcfa3f36 (stage 1) | 54 | 2195 | 371 | 3832 | flat | 201 | 54 / 43 / 275 |
| **1878bef03 (stage 2)** | **54** | **2205** | **313** | **3743** | flat | **145** | **54 / 43 / 216** |
| stage 2 vs stage 1 | 0 | +10 (band ±25) | −58 (−16%) | −89 | — | −56 KB (−28%) | run −59 |
| since the start | −47% | −2% | −29% | −4% | — | **−36%** | run −28% |

The coder's pin (a goal of 8 sets run again 227,552 → 189,456 B) is the same direction; the Condition goal's run is −28%. By allocation ticks, `kind.empty` falls from 26 KB to 1.7 KB per run and `type` is out of the top: the minting of section F is gone for this goal. What is left at the top of a run's ticks: `data` 15.5 KB, strings 14.8, a closure class (`<>c__DisplayClass32_0`) 9.6, the variable store rows 7.8, `Task<data>` 6.6, **`type.item.serializer.json` 5.5 (a serializer made per run)**, `Func<property,bool>` 5.2 (the unknown-property check's lambda in `Instance()`), `Func<string,(string,string,View)>` 4.3 (a delegate made per call).

## Findings

1. **`list.Count` can now throw. Medium.** `type/item/list/this.cs:286` `Count => new(Type.Types![typeof(number)], CountRaw)`. `Types` is null "for a type that is its identity alone" (`type/this.cs:873`), and the list's public constructors take any type. A probe (reverted) at this hash: `new item.list.@this(new type("list")).Count` and the same with a class-built type both throw `NullReferenceException`; before the stage they answered the count. No production site I found builds a list that way (`git grep` for `list.@this(new …type.@this` is empty), so it is a latent fault, not a failing path; the C# suites being green fits that. The `!` is the tell: either the list asks its own app's number type through a door that cannot be null, or `Count` keeps working when it has none (the number it makes holds its own operator type), and a pin builds a list from a bare type and reads `Count`.
2. **Pattern 5/7 — `new type(...)` sites left, and whether each is fair.** 127 sites in `PLang/app` (127 `new …type.@this(` lines). By kind:
   - *Fair, rare:* the `reader.Null()` null-row births in the serializers (about 25: `goal`, `step`, `action`, `dict`, `list`, `date`, `datetime`, `duration`, `guid`, `image`, `identity`, `snapshot`, `choice`, `clipboard`, `input`, `code`, `error`, `output`…): a null row is read once and the coder moved the two that were hot (`bool`, `type`). The pick list (`goal/step/pick/list/this.cs:664–696`) and the formal reader are build time. `error/this.cs:27` stays (an error is often born with no app: fair, and it says why).
   - *Not fair, hot — the same mint as number's was:* `bool/this.cs:30` (`private @this(bool value) : base(new type(typeof(@this)))`, every implicit bool); `number/serializer/Reader.cs:30` (every number read: `types[new type("number", number.Kind.Name), ctx]`); `dict/serializer/Reader.cs:24` and `list/serializer/Reader.cs:24` (a type probe per dict/list read); `number/this.Arithmetic.cs:46` (a climbed result); `choice/this.cs:43` (every choice birth); `duration/short/this.cs:12`, `duration/this.cs:64,88`, `duration/serializer/Reader.cs:23` and `image/*` (a probe per read); `data/this.cs:627` (`type ?? new type(typeof(T))` per `Create`). For the Condition goal they are not on its path (its ticks show 1.7 KB of `kind.empty`), so stage 2's bench cannot see them; a goal that reads a number or a dict or a duration pays one per read. These are the next sites for the stage, not a reason to fail it.
   - The list-probe shape `list[new type(name, kind), ctx]` (about 40 sites) makes a throwaway type to ask the list for the one it keeps; `type.Kinded(kind)` (the door stage 2 added, `type/this.cs:878`) is the question those sites should ask: a name and a kind, no object made to ask it.
3. **Pattern 12 — `Depth`/`Count` make a number per read, from a reference. Note.** `call.Depth => new(_stack.Number, _depth)` and `list.Count` make one small number per ask where before it also made a type: fine, and the stage's point. `Push` compares `_depth` (an int) instead of reading `Depth`: correct and one fewer allocation per push.
4. **Stage 1 lows, closed.** (a) `LazyInitializer.EnsureInitialized` in `Errors`/`Tags`/`Parameter`: one idiom (and `goal.Broken` uses its three-ref form, a second shape of the same call: acceptable). The lambdas `() => new(_stack.Errors)` capture `this`: a closure object per first ask, on the path the stage made lazy; a static helper or the method-group form would not allocate it. Low. (b) `step.Error`'s setter tells its goal (`Changed()`), which clears the judgment under a lock: the producer-side fix I asked for; `Changed()` takes a lock on a `_judging` object made on demand — heavier than the problem (a build-time write), but only the build pays it. (c) the `Cache` summary no longer promises a swap.
5. **Return early.** The touched regions guard first (`Push`, `Rest`, the readers' `if (reader.Null()) return …`). No wrapped-rest nesting found in the diff's methods.
6. **Value answers equality.** No raw-form compare added.

## Verdict v2: **PASS** — tester next. Finding 1 should be a pin and a one-line fix with the next stage that touches `list/this.cs`; finding 2's hot sites are the stage's remaining work (bool, number reader, dict/list readers, `data.Create`), measured on a goal that reads them.
