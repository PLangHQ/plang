# codeanalyzer — bugs, review 2 (dd6c19ece → b066bf3c5; code at 4b23498ee)

**Verdict: PASS** — F1–F8 fixed as ruled (F7's `Actor!` and F8's tuple stay per the architect). Tester and security next.

## Root birth +48 KB, attributed (bench, birth only, 2 rounds each)

| Commit | Root birth | Root KB |
|---|---|---|
| 3a29633f8 (parent) | 18.28 / 18.27 ms | 2364 / 2363 |
| a7cda5e3c (before stage 1) | 18.46 / 19.02 | 2364 / 2363 |
| 6fabe6db0 (stage 1: IAnswer, Live, Saved, `answer`, `test.setting.llm`) | 18.33 / 18.28 | 2386 / 2377 (+14…22) |
| 9f31620f3 (stage 1: `exchange` and the list file) | 18.92 / 19.31 | 2406 / 2405 (+20…29) |
| dd6c19ece (stage 7, the tip) | 18.95 / 18.05 | 2406 / 2410 (+0…4) |

It all arrives in stage 1: the two new module types (`answer`, `exchange`) and the shipped `IAnswer` entry, +42 KB in
two steps. Stages 2–7 (channels born of their list, `Wiring`, `actor.context.Run`) add nothing measurable, so a list
reference per channel is not it. Explained by the stage-1 decisions (architect 4–8): not a finding. Sub birth is 0.14 ms /
109 KB at every commit. Beside packages-tip (same machine class, the first bench run): sub 0.14 ms / 109 KB vs 0.14 / 109;
sub request 1.85 ms / 543 KB vs 1.85 / 539; root request 17.61 ms / 3857 KB vs 17.02 / 3821 (inside the band).
Time never leaves its band.

## The fixes, read at the diff and whole where they moved

- **F1** (e085d9bf6): `Shown.Apply(string)` reads `{"rects"…}`/`{"rect"…}` into `Part`s (a frame it cannot read is
  BadFrame with the cause), and both forms go to one private door `Put(Part[], max, move)`: bound every part, then decode
  (the parallel body only rents a buffer within the bound and calls `Qoi.Decode`, which returns false rather than
  throwing), then cut under the lock. Window has no `Parallel.For` left; `Window.Apply` is two lines. Root-cause fix at the
  producer, one applier instead of two.
- **F2**: the view reports a refused frame to the actor's error channel, as the window does.
- **F3** `query.IsStreamed()` (Saved, Live; OpenAi's older two stay, bugs 23). **F4** `screen.Max(context)` on the base,
  asked by window, view and process. **F5** `Fits` bounds by `Math.Min(max, Array.MaxLength) / 4`, so `w*h*4` is an int
  wherever it is counted. **F6** one `start.Refused(why)` Error; Configure's early returns dispose the app (checked:
  `plang one two` and `plang --test=5` print the ArgumentRefused line, exit 1). **F7** `InvokeGoal` answers a closed channel
  in the writer's flow. **F8** the summary names three homes.

## Measured

- Mutations (announced, reverted, status clean): `Shown.Put` without its bound loop → 4 `WindowFrameTests` red (binary and text past the max, a huge-max array case, the text `rects` case); the view's report removed → `TheView_ReportsAFramePastTheMax…` red.
- Green at the tip: WindowFrameTests 10/10, ShownTests 3/3, StartRefusalTests 13/13, ExecutorTests 33/33, PngTests 5/5;
  `plang --test` (llm included, no key) 305 total, 300 pass, 1 fail (the parent's), 3 stale, 1 skipped.

## Not blocking

- `Shown.Apply(string)`: a JSON `null` in the base64 slot reaches `Convert.FromBase64String(null)` (ArgumentNullException,
  not in the catch filter). It is an `ArgumentException`, which the window's decode loop skips, so the host stays up; it
  just isn't a BadFrame like its siblings. One word more in the filter.
- Executor's `Refused(error)` builds the error on the app's context before disposing the app; it prints correctly today.
- The view and the window each carry the same `error => _ = context.Actor.Channel.Report(context.Error(error))` lambda.
- The Windows run (APatchBeforeTheFirstFrame) and the json writer's setting are still not in a review.
