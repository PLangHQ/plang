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
