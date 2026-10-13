# security: app-systems

Read `common.md` beside this file first, then your character's threat model (PLang is user-sovereign).

## Why

No security pass has run on this line since runtime2 (2026-06-05): eight branches, the permission model, a new sandbox for the terminal, a settings store per actor and signed Data on the wire all landed without one.

## Areas, in this order

1. **Permissions and `Authorize`:** `PLang/app/type/item/path/this.Authorize.cs`, `FilePath.AuthGate`, the asks. The os/ write hole was closed in decision 382 (programs get Read/Execute under `os/`, Write/Delete only the system actor); verify it holds, including from a child app. Trust by origin; the os-folder ask; standing grants (in memory for the run, never persisted).
2. **The terminal's sandbox:** Landlock (`terminal` module); on a kernel without Landlock the 16 permission tests skip and production fails closed. Verify "fails closed".
3. **Secrets:** the `[Sensitive]` attribute (masked `******` in debug) and the `sensitive` type (masked `****`) are two mechanisms; unifying them is carried (next-branch.md, 681), so don't re-raise the duplication. Do check that neither leaks a value through any writer, the debug channel, an error message, a test report or the wire. The llm keys are read through settings (`%!llm.decider.setting.key%`, `llm.setting.Asked`).
4. **Signing:** sign-if-missing inside `Wire.Write` (`PLang/app/data/Wire.cs`); identity keys (`identity` module), `%Identity%` (the actor's own key, else the system's). Can unsigned data pass as signed, or a signature be replayed onto other content?
5. **The settings store:** rows `<actor>!<path>` in `/.data/data.sqlite`. Can one actor read or write another's settings? (Who may name another actor is an open design question with Ingi, 708; report what is possible today, not what it should be.)
6. **Bans:** `System.IO.*` and `Console.*` in production C# (CLAUDE.md; PLNG002 is a build error). Look for reaches that bypass the gate another way.

## Known, not findings

Rotation of the keys that leaked into transcripts was skipped by Ingi. 312 (a goal written out showing current values) was ruled not a leak (690). Never print a key, token or secret value, in your report or anywhere; say which file and that a value is present.

## Output

`security-report.json` at `.bot/app-systems/` as your character describes; severity under the user-sovereign model; **blocks the merge / carry** for each.

## In plang

The same brief as steps. The comments under it show what a finding looks like.

```plang
Security review of app-systems before the merge to runtime2 (decision 718)
Start
- read .bot/app-systems/architect/briefs/common.md, then your character's threat model (PLang is user-sovereign)
- review each area below in order; blue team first, then try to break it
- area: permissions and Authorize — PLang/app/type/item/path/this.Authorize.cs, FilePath.AuthGate, the asks, trust by origin, standing grants (in memory for the run, never persisted); the os/ write hole was closed in decision 382: check it holds, from a child app too
- area: the terminal's sandbox — the terminal module's Landlock; without Landlock its 16 permission tests skip and production must fail closed: check it does
- area: secrets — the [Sensitive] attribute (masked ****** in debug) and the sensitive type (masked ****); check neither leaks a value through a writer, the debug channel, an error message, a test report or the wire
- area: signing — sign-if-missing in PLang/app/data/Wire.cs, the identity module, %Identity%; can unsigned data pass as signed, or a signature be replayed onto other content?
- area: the settings store — rows <actor>!<path> in /.data/data.sqlite; can one actor read or write another's? Report what is possible today
- area: the bans — System.IO.* and Console.* in production C# (PLNG002 is a build error); look for reaches that bypass the gate another way
LIMITS: read only; no fixes; never print a key, token or secret value anywhere, in your report or a message: name the file and say a value is present
DONE WHEN: every finding has its area, how to reach it, severity under the user-sovereign model, and blocks or carry
REPLY: security-report.json at .bot/app-systems/, and a message in the Done/HASH/CHECK/OPEN shape
WHY: no security pass has run on this line since runtime2 (2026-06-05); the permission model, the terminal's sandbox, a settings store per actor and signed Data on the wire all landed without one

/ known, not findings
/ - two masking mechanisms, [Sensitive] and the sensitive type: carried (next-branch.md, 681); check for leaks, not the duplication
/ - the keys that leaked into transcripts: rotation skipped by Ingi
/ - 312, a goal written out showing current values: ruled not a leak (690)
/ - who may name another actor in a setting: open with Ingi (708)
/
/ the shape of a check, in plang (each as a user program, not the system actor)
/ - save "x" to file '/os/system/builder/Build.goal'
/   must be refused or asked, never written
/ - write out %!llm.decider.setting.key%
/   must never show the key itself
/ - call a goal that fails on a value holding a secret, on error call Show
/   Show: - write out %!error%      the error must not carry the secret
```
