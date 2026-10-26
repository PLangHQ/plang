# docs v1 — CLAUDE.md lines the code contradicts (found by doc2)

Each checked against the code first.
- Handler naming: an action is one partial class named for the action, `[Action("set")] public partial class Set` (`module/variable/set.cs:16`, `goal/call.cs:12`, `math/add.cs:6`); `git grep 'class \w+Handler'` in `PLang/app` finds none. Bullet renamed "Action naming" and corrected.
- Folders: `ls PLang/app` shows PascalCase `Attributes` and `Utils` only. The vocabulary list was stale too (`variable`, `filesystem`, `format`, `tester`, `config`, `callstack` are not folders of `PLang/app`); replaced by the folders that exist. The `app/filesystem/Default/` carve-out names a folder that does not exist (`find PLang -type d -name Default` is empty); removed.
- `this.Schema.cs`: the `Return` property is at `:65–71` (its doc comment `:65–69`, the property `:71`), not `:61`.

Not changed: the "Seven engine concepts" sentence and the two action renames, which I did not check.

## Old doc, same branch
- `Documentation/v0.2/bans.md:140`: the Console rule's channel call was `app.CurrentActor.Channels.WriteTextAsync(...)`; neither `CurrentActor` nor `WriteTextAsync` exists in `PLang/app`. It is `context.Actor.Channel[global::app.channel.list.@this.Output].WriteText(...)` (`module/build/code/Default.cs:218`, `module/llm/code/OpenAi.cs:399`; `channel/this.cs:147`), as CLAUDE.md says.
