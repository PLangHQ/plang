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
  Descend/Enumerate throw an AppException with its Error, Convert answers NotConvertible.
- Boy scout: the test runner's crash answers `Error.FromException` (546c3b072).

## In progress / next
- Stage 4: the goal channel runs its goal in a context made from the writer's (`writer.Child(channel's actor)`,
  the message in an isolated first frame). Waiting on plang-tester's minimal race goal for the red plang test.
- Stages 5–7 per the plan.
- With Ingi: OpenAi.cs's older smells (the envelope cache entry); a readable json file writer (the six answer files
  are rewritten from themselves when it lands).

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
