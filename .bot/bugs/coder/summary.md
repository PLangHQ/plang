# bugs — coder summary

**Version:** v1

## What this is
The bugs no branch owned after item-doors, fast-apps and packages: the auditor's N-list, the goal channel's race,
packages' Windows window, and Ingi's three yeses. The plan is the architect's, at `test/plan/bugs/start.md`
(`start.goal` its index), seven stages, each red first. Decisions are numbered in `.bot/bugs/architect/summary.md`.

## What was done
- **Stage 1 — a test's llm answers come from a file** (6fabe6db0, 9f31620f3). The request goes out through one
  `module/llm/code/IAnswer`, chosen once for the app: `Live` (OpenAI's endpoint and key; the key asked only when a
  request goes out) is shipped; a test registers `Saved` on its own app when `test.setting.llm` is `saved`
  (`choice<module/llm/type/answer>`, default). `Saved` reads the test's `X.test.llm.json` as `list<exchange>`
  (`module/llm/type/exchange`, `{request, response}`) through `path.Read`, matches by `data.Compare`, asks a
  missing one live once, and on a pass `Keep`s the file to what it asked (`path.Save`). `live` ignores the file.
  The six llm tests' answers are recorded; every plang test now runs without OpenAI.
- **Stage 2 — consent answers what really happened** (8e6888aba, ac584f4e1, 4ac260ffb). N1 `identity.export`
  refuses (ExportWaitsForCriticalActions) before any ask. N4 the role's standing grants and the run's "y" grants
  are two lists; N3 `Add` answers its save. N5's fixture really saves its grant — which exposed a hole: a saved
  grant on `.data` opened it; the stores' folder now opens only by a grant the role stood on the store itself
  (`permission.Stands(folder, …)`, also closing security's S1). N8 only nobody-could-answer is a denial; any other
  exception answers as itself.
- **Stage 3 — readers answer their errors** (add1f6d30, cdc347f82). N10 json `Number()` checks its token. N11 size,
  parallel and choice decline with `DeclinedException` (SizeInvalid / ParallelInvalid / ChoiceInvalid); kind's
  Convert answers NotConvertible. kind's base Descend/Enumerate stay plain exceptions (5aaa05258): no plang step
  reaches them, so they are plang-is-broken guards.
- Boy scout: the test runner's crash answers `Error.FromException` (546c3b072).

- **Stage 4 — a goal channel runs its goal like a call** (fa3e9272b, 3edb17b4e, c74ec9771). One door,
  `context.Run(actor, then)`: right here when it's the context's own actor, else a child made from it. The goal
  channel (the writer's flow) and `goal.call` with a named actor (`actor.list.Use`) both go through it; the race
  (19/20 writes answered with another's message) is gone. Proven in C# and by the two plang tests the builder built.
- **Stage 5 — the command line refuses what it can't read** (2f3bffd46, a45546a5c). ArgumentRefused naming the
  argument and the form that works: a second goal, a setting written as a goal, a mode word given a value, a mode
  turned off beside its setting, one property in two cases. `%path%` is `app.filesystem.root`.
- **Stage 6 — paths and the Windows window** (362cd6c03, 22faad482, 89fd7afdc, db0339f00). N15: `path.Holds` is the
  one containment rule, asked by IsUnder and ValidatePath (a sibling `/root2` is no place in `/root`). N17:
  OsTrustTests asks IsUnder. The window holds a `Shown`: its own frame code and QOI decoder are gone; `Shown.Put`
  and `Shown.Apply` bound each rectangle by `screen.setting.max` (`Bitmap.Fits`, the one rule) before any decode,
  which closes the Linux view's unbounded w*h too; an offer is `(frame, max)` and the window reports a refusal its
  one way; the `_max` mirror is gone. Linux green and a win-x64 build; the window waits for the Windows run.
- **Stage 7 — Ingi's three yeses** (66f0456c0, 363bfd5da, 9746789e1, 93495fdf8, ad0caedd7). `module.Remove` answers
  `data<bool>` (ModuleShared answered, not thrown). The console flag is `app.Wiring { console, none }`. A channel is
  born of its actor's channels: the base takes its list, `Actor` is the list's, Register and Set guard a channel born
  elsewhere, and every late `Actor =` stamp is gone. The surface golden was re-pinned for each, naming fast-apps N1–N3.
- Also: the `| json` filter escapes `< > &` itself, so a page stays safe whatever the json writer writes (9f65bf639).
- **A page escapes what it writes** (bugs 44: ca721e342, 1886e9f71, fb65c2064, 204b37559). `ui/code/Fluid.cs` picks
  its encoder by the template format's own mime from `_encoder` (text/html, image/svg+xml → HtmlEncoder); any other
  template, and an inline one, writes values as they are, so the builder's prompts are unchanged. callGoal's answer
  goes through the same encoder. `| json` is written unencoded (it belongs in a script, never an attribute); `| raw`
  is new (Fluid 2.40 has none) and writes a value through its own write, unescaped. The render's catch-all now holds
  the fault whole as its cause (`list = [Error.FromException(ex)]`).
- **From the Windows run** (bugs 46–47):
  - 003494d74: `call/list` Push refuses a frame as CallStackOverflow also when the thread's stack runs short
    (`RuntimeHelpers.TryEnsureSufficientExecutionStack`): 1500 frames fit 8 MB, not Windows' 1 MB, and an overflow
    ends the process. StackDepthTests runs a self-calling goal on a 256 KB thread. The exception's `MaxDepth` is `Depth`.
  - cf8558c25: `LlmTestHelper.Mock(app, handler)` is the one door for a test's llm HTTP mock; it gives the app a
    test key, so no mocked test needs OPENAI_API_KEY (83 were red without it).
  - aa58ceb9b: OpenAiImageDenialTests names its out-of-root file in plang's OS-rooted form (`//tmp/x` on Unix), so
    the denial is really reached on Linux.
  - d2abb8292: a framed stream channel's `End` is `"\n"` on every OS (was Environment.NewLine, so on Windows LF
    lines never read); CR LF still reads as one end.

## In progress / next
- The json writer setting (`%!type.json.setting.format%`, `formatted | compact`): its shape is approved, but it
  waits on Ingi's answer about a json-lines kind for the line writers (errors.jsonl, the headless cursor line, json
  channel lines). Then the six `.llm.json` files are rewritten from themselves, no OpenAI.
- The Windows run (the window, APatchBeforeTheFirstFrame, the path probes), and the host's rerun of d2abb8292.
  Left for the windows branch: Test_Complete's ≥1 ms duration, the 152 KB first-ask bound, and
  `terminal/type/process/this.cs:232` (a child's stdin line with the OS newline).
- The gate: the llm tests once live (`--test.setting.llm=live`), and the full gate.
- Kept for llm-provider by Ingi: OpenAi.cs's older smells (the envelope cache entry, its static helpers).

## Code example
```csharp
// test/this.cs — the test sets up its own app; the send site has no `if testing`
if (setting.Llm.Value == global::app.module.llm.type.answer.saved)
{
    var saved = new Saved(Goal.Path!, app.Code.Get<IAnswer>().Provider!);
    app.Code.Register<IAnswer>(saved);
    app.Code.SetDefault<IAnswer>(saved.Name);
}
```
