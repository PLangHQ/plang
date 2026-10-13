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
