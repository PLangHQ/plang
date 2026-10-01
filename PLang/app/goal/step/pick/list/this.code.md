# step.Pick: the decider's answer for one step, and the check of the LLM's answer against it

`step.Pick` (`PLang/app/goal/step/this.cs:71`, built lazily, never stored in the `.pr`) is the build-time state of one
step between the decider's answer and the LLM's: what the decider picked, what prompt C lists for the step, the
starting line it shows, and the check of the answer that comes back. A cached step and a step written in formal take
nothing (`Take`, `this.cs:178`).

## Where it enters

```
BuildGoal/Decide.goal  llm.decider (stages 1, 2)  →  build.pick                PLang/app/module/build/pick.cs:28
└─ Builder.Pick                                                                  PLang/app/module/build/code/Default.cs:242
   └─ foreach step: step.Pick.Take(answer, popular, context)                     this.cs:174
      ├─ :181-193 the answer's entries for this step (keys "s<i>_<id>", Key :51) into
      │           _module (@module), _popular (@popular), _also (@also.X), _branch, _named (a common action),
      │           _choice (a module's action)
      ├─ :194  _items     = Picks()        every scored pick                     :429
      ├─ :195  _question  = Questions()    stage 2's questions for the step      :457
      ├─ :199  _listed    = Listing()      what prompt C lists, with marks        :258
      │        (certain ≥ 0.9, possible ≥ 0.5, the write-to's variable.set, popular on an unsure step)
      ├─ :200  Formal     = Prefill()      the starting line                      :294
      └─ :201  Known      = Code()         the code the certain picks already know (read by step.Scope) :315
```

Prompt C's user message renders it per step (`os/system/builder/llm/templates/properties.template`):
`=> decider:` from `Listed`, `=> formal:` from `Formal`. The decider's own templates read `Question`, `Module`,
`IsCondition`, `IsUnsure` (`decider.state.template`, `decider2.template`).

## The starting line (`Formal`)

`Prefill` (:294) places each certain action through its own `action.Prefill(line, Call(action))` into a
`pick.line` (`PLang/app/goal/step/pick/line/this.cs`): a loop leads, a clause follows its action, a lone if holds the
step's other actions in `{ }`, a keep (`variable.set`, `action/keep/this.cs`) follows what produces a value.

`Call` (:351) writes an action with its **required properties by name alone**, `file.read(Path)`: a slot to fill,
nothing a model could copy as a value. Measured (5 full rounds each, replayed picks): `Name=?` lost whole goals
(`goal.call(Name=?)` copied, refused twice); `Name: type` was copied as the value for a `goal` slot
(`goal.call(Name="goal")`). A slot copied as it stands is refused by the formal reader naming the fix
(`action/formal/reader.cs`, Row: "`Path` is a property still to fill").

## The check (`Agree`, `Unlisted`)

`goal.step.list` reads the LLM's answer and asks each step's pick (`PLang/app/goal/step/list/this.cs:144`, `:206`):

- `Agree(code)` (:210): refused — a certain pick left out, an action not listed, a held action not listed (goal.call
  excepted), the `write to %x%` variable not written; warnings — a possible or popular pick used.
- `Unlisted(names)` (:237): the actions named that the decider did not list.

## Known faults

- **English word readings.** `As` (`as %x%`, :94), `WriteTo`/`Destination`/`Writes` (`write to %x%`, :149-161),
  `Assigns`/`AssignsOne`/`Target`/`Assigned` (`%x% = …`, :96-121) read the step's words with English regexes. plang
  is written in any language; these readings don't hold for it. They feed `Known`, the write-to keep in the starting
  line, and the write-to check. To be replaced by what the LLM's answer says.
- The first answer for a `foreach %x% as %y%` step often leaves out `Item` (3 of 3 builds); the retry restores it.
- **Not refused any more: an answer that drops a whole `on error call X` clause.** The check "step N calls X, but no
  action calls it" caught it, and went because it read `call X` from the step's English words. A dropped clause now
  builds; what refuses it next must come from the answer and the decider's picks, not from the words.

## Tests

The prompts this part renders are pinned as fixtures (C# is the reference; an intended change re-pins a fixture
through its test's `[Explicit]` `AcceptTheFixture`, run by name: `PLang.Tests.Wire --treenode-filter
"/*/*/PickListTests/AcceptTheFixture"`):
- `PLang.Tests/Wire/App/Decider/PickListTests.cs` — `pick_golden.json` (stage 1 and 2 requests and states, prompt C's
  user message, the picks), `settings_golden.json` (prompt C's Settings and Keys blocks);
- `LineTwinTests.cs` — `line_golden.json` (the starting line's placement);
- `ConfirmTemplateTests.cs` — `confirm_golden.json` (the number confirmation).
