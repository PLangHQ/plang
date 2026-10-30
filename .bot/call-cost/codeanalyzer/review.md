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

---

# Review v3 — call-cost stage 3 (a62c2da21..b274f0406)

Patterns first: 11 (lazy), 5/7 (found, not made / one door), 12 (one answer). Touched files: `actor/setting/this.cs`, `call/this.cs`, `goal/list/this.cs`, `shortcut/this.cs`; the diff's methods read whole, the invalidation points of each kept answer checked.

## Bench (`run.sh` birth + request, `Bench repeat`/`phases`; bytes load-independent, load 5–6)

| | Sub birth | Root birth | Sub request | Root request | Held | A goal run again (`repeat`) | Request born / load / run |
|---|---|---|---|---|---|---|---|
| 77add2895 (start) | 101 KB | 2260 | 441 | 3897 | flat | 227 KB | 98 / 43 / 299 |
| a62c2da21 (stage 2b) | 54 | 2206 | 306 | 3729 | flat | 145 | 54 / 40 / 211 |
| **b274f0406 (stage 3)** | **54** | **2194** | **284** | **3713** | flat | **109** | **54 / 40 / 192** |
| stage 3 vs 2b | 0 | −12 | −22 (−7%) | −16 | — | −36 KB (−25%) | run −19 |
| since the start | −47% | −3% | −36% | −5% | — | **−52%** | run −36% |

Everything in band or below; held flat. The coder's pins (an option ask 1,856 → 160 B, a match 5,520 → 0, a frame 1,544 → 680 B) agree with the repeat row.

## Findings (all low; none blocks)

1. **Pattern 12 — the lazy idiom is back to the `CompareExchange` form. Low.** Stage 2 closed my stage-1 finding by moving `Errors`/`Tags`/`Parameter` to `LazyInitializer.EnsureInitialized`; the two pieces stage 3 makes lazy, `property` and `Children` (`call/this.cs:36`, `:128`), are written `_x ?? Interlocked.CompareExchange(ref _x, new(...), null) ?? _x` again. Five lazy pieces on one class, two idioms. Take `LazyInitializer` (as `call.list._audit`) for the two new ones so the class answers "made at its first ask" one way.
2. **Kept settings answer: correct, with two notes. Low.** (a) The answer is read at a `version` taken *before* it is worked out, and the cache is not written while any actor's rows up the chain are unread (`IsRead`), so a write during the work leaves it stale-and-recomputed, never wrong; the invalidation points (`Set`, `Save`, `Remove`, and the clone path) all call `Move()`. (b) The kept `data` (usually the same "not set" `NotFound` error Data) is one object returned to every asker of that scope. A caller that writes a property onto the answer writes onto the shared one; the generated `Resolve` only reads it (`.As<T>()`), so nothing does today. If an asker ever should own it, it asks for a copy. A pin that sets an option, asks, sets another option anywhere in the chain, asks again and sees the new answer is what protects the version, if it is not in the stage's pins already.
3. **`goal.list` kept finds: one mutation point, which is why it holds. Low.** `_found` is cleared only in `Add` (`goal/list/this.cs:188`), and `Add` is the only writer to the list (`base.Add` at `:185`; no `Remove`/`Clear`/`Replace` in the class). If a removal is ever added, it must clear `_found` too; the comment says the gate order but not that rule. The per-folder dictionary is keyed by `path` (its own `Equals`/hash), fine.
4. **Pattern 5 — `shortcut.Name` is a birth fact. Closed**, and `Path.Stem.ToString()` at birth costs one string per shortcut (6 per app) against 5,520 B per match before.
5. **Return early.** The diff's methods guard first (`Get`, `Found`, `In`); no wrapped-rest nesting.
6. **Value answers equality.** `found.In` compares folders through `path.Equals`; no raw-form compare added.

## The `Func<string, (string,string,View)>` the coder asked about (4.6 KB a run in the ticks above)
It is `type/reader/this.cs:101`: `Token(typeName, kind, view) => Find(_runtimeTyped, Shipped.Tokens, k => (typeName, k, view), kind)`. The lambda captures `typeName` and `view`, so every reader lookup allocates a closure (`<>c__DisplayClass11_0`, 2.25 KB in the ticks) and a delegate; `Find` calls it twice (`exact`, `any`). The sibling at `:97` (`Payload`, `k => (typeName, k)`) is the same shape with `Func<string,(string,string)>`. The key a lookup builds is data, not a function: `Find` can take the two keys (`(typeName, kind-or-any, view)` and `(typeName, AnyKind, view)`) and no delegate or closure is made. Both are on the path of every typed read (a number, a bool, a dict).
Others at the top of a repeated run's ticks, unnamed: `<>c__DisplayClass32_0` 7.7 KB (a closure class, method not found by text), `Func<property,bool>` 4.8 KB (`Instance()`'s unknown-property check, `goal/step/action/this.cs:365`), `type.item.serializer.json` 4.7 KB (a json serializer made per run; site not found).

## Verdict v3: **PASS** — tester next.

---

# Review v4 — call-cost stage 4 (b274f0406..9a96736d1), and stage 5's property-bag measurement

Patterns: 11 (lazy), 5/7 (found, not made), 12. Touched: the generator (`Emission/Action`, `Emission/Property`), `goal/step/action/this.cs`, `actor/context/this.cs`, `data/this.cs`, `type/reader/this.cs`, and the `View`/serializer callers of `context.Json`.

## Bench (`run.sh` birth + request, `Bench repeat`/`phases`; bytes load-independent, load 7.6)

| | Sub birth | Root birth | Sub request | Root request | Held | A goal run again (`repeat`) | Request born / load / run |
|---|---|---|---|---|---|---|---|
| 77add2895 (start) | 101 KB | 2260 | 441 | 3897 | flat | 227 KB | 98 / 43 / 299 |
| b274f0406 (stage 3) | 54 | 2194 | 284 | 3713 | flat | 109 | 54 / 40 / 192 |
| **9a96736d1 (stage 4)** | **54** | **2203** | **246** | **3647** | flat | **69** | **54 / 40 / 151** |
| stage 4 vs 3 | 0 | +9 (band ±25) | −38 (−13%) | −66 | — | −40 KB (−37%) | run −41 |
| since the start | −47% | −3% | **−44%** | −6% | — | **−70%** | run −50% |

The coder's pins (an action started again 14,272 → 11,488 B; a reader lookup 264 → 0) agree. A goal run again is now 69 KB, down from 227.

## Findings

1. **The `<>c__DisplayClass32_0` the coder asked for is `type.Empty` (`type/this.cs:173`), 176 calls a run.** A probe (counter in `Empty`, reverted) counted **176.2 calls per goal run** and **0** for the parser candidate. `Empty` memoizes (`if (_empty is { } empty) return empty();`), but its lambdas capture `typed`, `ctor`, `first` and `this`, so the compiler makes the closure object **at method entry, before the early return**: 176 × 48 B ≈ 8.4 KB, the 7.7 KB in the ticks. Fix: the memoized path takes no closure — split the method (`Empty` returns `_empty` when made; a private method that builds the delegate holds the captures), or keep the constructor and `first` in fields. After stage 4 the same name is 2.7 KB in the ticks (fewer calls), so it is still there. Pattern 11: a piece made by its first asker, not at entry.
2. **The same trap in the new `Unknown(element)` (`goal/step/action/this.cs:394`). Low.** `_unknown ?? CompareExchange(ref _unknown, Property.Where(p => element.Property[p.Name] == null).ToList(), null) ?? _unknown`: `element` is a parameter captured by the lambda, so a closure is made on every call, including the calls that return `_unknown`. ~32 B × ~11 actions a run: small, but the idiom for a once-judged fact should not allocate on the judged path. `Func<property,bool>` is still 3.6 KB a run in the ticks after this stage; this method is not the whole of it (a delegate is made only when the lambda is evaluated, and `_unknown` is set), so there is a second site I have not named. I can find it with a counter if wanted.
3. **The frame's lazies and my stage-3 note.** At 9a96736d1 all five frame lazies (`property`, `Errors`, `Children`, `Tags`, `Parameter`) are `Interlocked.CompareExchange`, as the coder said: my "LazyInitializer for three" read stage 2's diff, which 2b changed back. Finding retired. The same one-line idiom is now also `Unknown`, `Resolver` and `context.Json` (eight sites); a one-line owner for it would name it, but it is allocation-free as written and a choice of the plan, so this is a note only.
4. **`Resolver` (idea 4): sound.** `element.Resolver` is made at the class's first run with its module's own System context (`Module.App.actor.list.System.Context`), never a run's, so no sub app is held (the rule in CLAUDE.md "a run never writes an object its app shares": the resolver is a cache every app over the module gets the same answer from). It never runs; `Resolve(action, context)` takes the run's own context for the handler it makes.
5. **Reader keys (`Find(exact, any)`): closed** — the lookup is 0 B, the site I named.
6. **`context.Json`:** one json reader per context, made at its first ask; replaces `new serializer.json(_context)` at two `data` sites. Correct (it holds only the context).

## Stage 5 — the property bag of a copy: not worth it; drop it
`Bench copy` (best of 7, this thread's bytes):
| a copy of… | bytes | time |
|---|---|---|
| a value that carries nothing (`set %y% = %x%`, a goal-call argument) | **112 B** | 0.13 µs |
| a value whose bag was made, empty | 248 B (+136) | 0.14 µs |
| a value carrying one property | 384 B | 0.25 µs |
The bag is already lazy: `Copy` takes `Carried?.Copy()`, and `Carried` is null until something asks (`data/attachment.cs`), so a copy of a plain value costs no bag. In a real run (probe in `Copy`, reverted; 503 runs): **~4 copies a run, every one with an attachment that holds nothing** (`withAttachment` = all, `withSomething` = 0), so each pays the 136 B for copying an empty attachment: **~0.5 KB of a 69 KB run (0.8%)**. A guard in `Copy` (copy the attachment only when `HasProperties || HasProperty`) would take it, for one line and nothing else. Not a stage: if anything, a one-line addition to whichever stage touches `data/this.cs` next. The reason for the log: stage 5's bag costs 0 B in the common case and 136 B in the empty-attachment case, ~0.8% of a run.

## Verdict v4: **PASS** — tester next.
