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

- **Stage 4 — a goal channel runs its goal like a call** (fa3e9272b, 3edb17b4e, c74ec9771). One door,
  `context.Run(actor, then)`: right here when it's the context's own actor, else a child made from it. The goal
  channel (the writer's flow) and `goal.call` with a named actor (`actor.list.Use`) both go through it; the race
  (19/20 writes answered with another's message) is gone. Proven in C# and by the two plang tests the builder built.
- **Stage 5 — the command line refuses what it can't read** (2f3bffd46, a45546a5c). ArgumentRefused naming the
  argument and the form that works: a second goal, a setting written as a goal, a mode word given a value, a mode
  turned off beside its setting, one property in two cases. `%path%` is `app.filesystem.root`.
- **Stage 6 so far** (362cd6c03, 22faad482). N15: `path.Holds` is the one containment rule, asked by IsUnder and
  ValidatePath. N17: OsTrustTests asks IsUnder.

## In progress / next
- Stage 6's Windows window: its shape (Window onto Shown and Qoi, the frame bound in Shown, each offer carrying its
  own decoding) is with the architect.
- Stage 7: Ingi's three yeses.
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
