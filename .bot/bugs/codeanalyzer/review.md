# codeanalyzer — bugs, review 1 (runtime2 3a29633f8 → ad0caedd7)

**Verdict: FAIL** — one blocker (F1, probed) and one swallowed refusal (F2); the coder is next. The rest are findings
by smell, none blocking alone.

## Measured

| Check | Result |
|---|---|
| Bench row `bugs-tip` (vs packages-tip) | sub birth 0.14 ms / 109 KB (same); root birth 18.45 ms (17.07; band ±1.5) / 2406 KB (2358; band ±25 → **+48 KB**); sub request 1.85 ms / 543 KB (539); root request 17.61 ms / 3857 KB (3821); held memory flat (2.1 → 2.1 → 2.1); `plang --test` 4.5 s, 294 pass / 1 fail (the parent's); sweep 45.2 s, red = Wire's six |
| `plang --test`, llm tests included, no key | 305 total, 300 pass, 1 fail (`BareLiteralJudgedByForm`, baseline), 3 stale, 1 skipped. The six llm goals pass from their files, offline |
| Mutations (announced, reverted, `git status` clean) | `Holds` as a plain prefix → ValidatePathTests 2 red (sibling cases); `Use` back to the named actor's root context → NamedActorCallRaceTests red; `Standing` with `Find` before the store check → StoreStandingProbeTests 1 red (`…TheUserStillCannotReadTheStoresKey`) |
| Probe (throwaway test, deleted) | `Window.Apply` with `{"rects":[[0,0,50000,50000,"AAAA"]]}` throws **`System.AggregateException`** |
| Not run | per-commit builds (cheaper rule); the Windows run; the json writer's setting (not in this review) |

The bench build part was skipped (no csproj change), but **`Tools/Bench/Program.cs` did not compile** against the branch:
it passed `autoWireConsoleChannels:`, which `Wiring` replaced (ad0caedd7's parent 93495fdf8 and 363bfd5da never touched
it). I moved it to `Wiring` (a consumer left behind; mine to keep, committed with this review).
Root birth +48 KB is outside its band; it is explained by the branch's decisions (`IAnswer` shipped in the code table,
a channel holding its list). Held memory is flat, so nothing blocks on it.

## Blocking

### F1 — Root-cause: a refused rectangle in a text frame ends the host (Window.cs:344, :166–168)
- **Symptom:** `Window.Apply`'s `rects` path runs `screen.Put(…, max)` inside `Parallel.For`. `Put` bounds first and
  throws `AppException(BadFrame)`; `Parallel.For` wraps it in an `AggregateException`. `Decode` (Window.cs:166–168)
  catches `AppException` and a list of types that does not include `AggregateException`, so it escapes the decode
  thread and ends the process. Probed: it escapes as `System.AggregateException`. The same wrap hits a bad base64
  in the same body (`Convert.FromBase64String`, :344), which the old bare `catch` (parent Window.cs:143) swallowed and
  the new filter lists as `FormatException` — also wrong inside `Parallel.For`.
- **Producer:** Window.Apply is a second multi-rectangle applier beside `Shown.Apply`. `Shown.Apply` bounds *every*
  part before its `Parallel.For`, so nothing can throw inside it; Window's text path bounds inside the parallel body.
  Two ways to apply a frame's rectangles (fork), one of them wrong.
- **Root-level fix:** `Shown` takes a text frame's parts through one door like `Apply` does (bound all, then decode in
  parallel, then cut under the lock), and `Window.Apply` hands it the parts; nothing parallel is left in Window.
  Test it through that door: `WindowFrameTests` only cover `Shown.Apply`, `Shown.Put` and the Linux view, never
  `rects` (Windows' own, the path that broke). This is the packages-F4 class again: a producer's refusal reaching a thread
  with no catch for it.

## Findings

### F2 — swallowed error, asymmetric with its sibling (view/this.cs:165) — Pass 4.5 #14
`view.Apply` answers a refused rectangle with `catch (AppException) { }`; the Windows window reports the same refusal
(`report(ex.Error)`, Window.cs:166). The Linux view's user never learns a frame was refused (a 50000×50000 claim and a
`--screen.setting.max` too low look identical to "nothing happened"). Report it where the view's errors go, as the window
does. (Also two empty catches on one `try`.)

### F3 — stray predicate, 2 new sites (Saved.cs:38, Live.cs:28; older OpenAi.cs:201,209 exempt)
`(action.OnStream == null ? null : await action.OnStream.Value()) != null` is "is this ask streamed", written three
times (twice new). It belongs on the query (`action.Streamed()`), asked once by the answer; Saved's "a streamed ask
goes live" then reads as the sentence it is.

### F4 — raw hand-off: `screen.setting.Max` read at three doors (window/this.cs:146, view/this.cs:93, process/this.cs:221)
Each of the three reads `(await context.Setting.Get<screen.setting>()).Max` itself (the window as a private static
`Max(context)` — also a stray helper). One owner: the screen asks its setting (`screen.Max(context)`), and
`process` calls `screen.Show(message, …)` per message — the setting is read **once per PlangOS message** on the frame
path (process/this.cs:221), not once per screen.

### F5 — Shown.cs:114 — `w * h * 4` is an unchecked int
`Bitmap.Fits` bounds pixels by `max/4` only; a `--screen.setting.max` above 2 GiB lets `w*h*4` overflow to a negative
`ArrayPool.Rent` (ArgumentOutOfRange, inside the Window's Parallel → F1's wrap). Bound by `int.MaxValue / 4` in `Fits`, or
document the setting's ceiling. (Shown.Put(Bitmap), :88–89, also keeps a `Math.Min` for sizes Window already checked equal:
dead guard.)

### F6 — Executor.Configure (Executor.cs:71, :76, :93)
Three inline `Error(new error(…, "ArgumentRefused", 400))` returns beside `start.Refused(…)` (a fourth way to the same
key): one door for "the line is refused" (`start.@this.Refused(why).Error`). Every early `return (null, …)` leaves the
just-made `app` undisposed (new with this branch for the `Parse` refusal: it used to refuse before the app existed).
`modes` is a naked `(Type Setting, Func<object?,string> Meant)` table: a mode type would own both.

### F7 — channel/this.cs:100 `Actor => Channels.Actor!` and goal/this.cs:115 vs :120
`Actor` is declared non-null but a service's channels have none (the `!` lies; callers get an NRE later, where the old
`null!` field did the same). In the goal channel `InvokeGoal` takes `data.Context.Error` at :115 and
`data.Context ?? Actor.Context` at :120: one of them is wrong (older :115; the file is touched, so it is this branch's).

### F8 — permission/this.cs:7–17 (stale doc), :24 (flat copy)
The class summary still says "Two homes" (session list, persisted); there are three now (`_standing`, `_once`,
persisted). `_standing` holds `(path Place, data Grant)` where the grant also holds that place's `Path`: a mirror to drift;
ask the grant. The `catch … when (ex is OperationCanceledException or IOException or ObjectDisposedException || ex is
AppException {Error.Unanswered: true})` (:88) classifies by CLR type in a general catch; the producer (stream input)
saying "unanswered" as an AppException everywhere would leave one clause.

## Older, in files this branch touched (boy scout; commit of origin: parent 3a29633f8 unless noted)
- Window.cs:161, :249 — the update queue is `object` told apart by `is byte[]` / `(string)patch` (type-switch);
  `Window.Field`/`Frame` string helpers (stray helpers); `public int Frames` mutable field; `shown` read unlocked by
  Paint (:640, documented as benign).
- test/this.cs:162 — Test knows the llm module (accepted, architect 5); `Complete` is called twice on a failed Keep.
- Executor.Configure is one 120-line method.

## Judged and clean
- `IAnswer`/`Live`/`Saved`/`exchange`: one sender chosen once, no `if testing`, file through `path.Read/Save` as `list<exchange>`, no header in the file. `Saved` serialises asks under a gate while the live ask is out: tests only.
- `actor.context.Run`: one door, `using` ends the child with the run. `Born` guard throws a plain exception for plang-is-broken, per the rule.
- `Holds`: one rule, three callers. `Permission.Stands`: the S1 fix is where the discipline lives; the mutation proves it.
- Readers (N10/N11): `DeclinedException` carrying its Error whole; the choice courier answers it whole; the cause is held, not cracked.
- Fluid `| json` escape: one place, after the writer, whatever the kind writes.
- `Wiring`: one place asks it (`origin.code.Console`). The surface golden carries every locked change with its decision (N1–N3, 4).
- `module.Remove` answers data; ModuleShared answered, not thrown.

Tracked elsewhere: obp-cleanup (none new). OpenAi.cs envelope/helpers: llm-provider (Ingi, bugs 23).
