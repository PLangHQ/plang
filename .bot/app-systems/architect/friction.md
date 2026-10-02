# Friction: reviews from plang's own users

The bots are the users of plang and its builder. We eat our own dog food (Ingi: "they are the user's og the plang builder, they should bring reviews … we can make it amazing with everybody's help"). Each entry is what's still **open**: a fixed item is dropped (its commit is in the decision log), and a wish plang already answers is answered by pointing the bot at the doc (Ingi: "if things have been fixed don't mention them, if there is already away to do things, teach the bot where he can find info and to check when building"). Each bot keeps its full review in `.bot/app-systems/<bot>/friction.md` (the educator's is on `educator-lesson`; the os bot messages its own).

Entry form: **who** · **what** · **cost** (evidence) · **if I had** (the wish).

## Seeing what plang wrote, and why

- **educator, architect · no plain view of a built goal.** There's no command that prints a goal's steps and their actions; `plang show` runs a goal named show (`PLang/Executor.cs:33–34`). (A stale python summariser was removed, Ingi.) **Cost:** ~15 min per lesson hand-building "what plang wrote"; issue 39's counts were wrong from a grep on compact JSON. **If I had** `plang show Start` (each step and its actions, one line each, in formal), every bot and every reader would see the build the same way.
- **architect, builder · no single view of a step's pick.** The pieces exist (`--debug={"goal":"Decide","variables":["state","questions","answer"]}` and the writer's `=> decider: file.read 0.99, …` line, `os/system/builder/debugging.code.md:30–47`), but no one table per step, and the decider's stage scores don't appear in `--debug={"llm":…}` at all (it isn't the OpenAI writer). **Cost:** issues 40 and 41 were pieced together from `%answer%` with jq and from reading `pick/list/this.cs`. **If I had** a per-step pick table (each candidate: stage-1 score, stage-2 answer, final mark, why kept or dropped), and a warning when the action the writer's notes point to is dropped for a certain common action that contradicts it.
- **builder · a common action outranks the module answer** (issue 41's shape; fixed by teaching, 0/5 → 5/5, bb92576d0). The module question answered window (0.70), but goal.call's stage-1 yes/no (0.94) is enforced over it, so a module's own call-like action can't displace goal.call; the teaching is now load-bearing. The writer kept reaching for `goal.call(…, Window=…)`. **If I had** the pick weigh the module answer when it disagrees with a common action's yes/no, or goal.call owning where a goal runs, the teaching wouldn't carry it. A design question for Ingi.
- **builder · a `--debug` key that's wrong doesn't say the right ones.** `"maxLength":50000` failed with `UnknownSetting 'maxLength'`; the key is `"length":{"max":…}` (debug.md lists them). **If I had** `UnknownSetting` name the valid keys, it would be a one-line fix.
- **educator · LLM tracing also turns on step debug of the builder's own goals.** `--debug={"llm":…}` registers the step and goal blocks too (`debug/this.cs:127–133`), and `"goal"` doesn't scope the LLM blocks, although `Documentation/v0.2/build.md:84` says it does. **Cost:** 885 KB for a 3-step goal. **If I had** `{"llm":…}` alone print only the exchange (and build.md corrected).
- **architect · a retry's LLM exchange isn't labelled.** Build output says `Properties rejected — retrying: <msg>` and debug shows `Step [i] of FixSteps`, but the LLM blocks are titled only SYSTEM/USER/RESPONSE. **Cost:** issue 39 was first read as "FixSteps' set fails". **If I had** the retry's blocks labelled with the attempt and its reason.
- **educator · no word on what a build did.** A cached goal returns silently; `build-output.template:14–15` defines `step-cached`/`step-fresh` markers no goal emits, and `build.md:65` shows a `[≡]` that never prints. **If I had** a closing line: "built 1, unchanged 2, from cache 0".
- **educator · a merge silently changes a working app** (the studio's TTS post became http.upload; `with sha256` dropped; found at run against a paid API). **If I had** `plang build --diff`: which steps' actions changed against the previous `.pr`.

## The builder misreading what a user wrote

- **educator · the same step builds differently between builds** (C4 6/6 → 4/6 → 6/6 across three commits; ~10 builds per lesson step). **If I had** `plang build --samples=5`: N fresh builds, which steps disagree and how.
- **educator · goal comments steer the build** (load vars 3/3 with an explaining comment, 0/3 without). **If I had** comments reach the writer as the author's background, never as instructions, so a probe is a fair test.
- **educator · goal.call's signature contradicts its notation.** `Parameter?: list` (`goal/call.cs:22`, `data<list>`) while every example writes a dict (`Parameter={to: %email%}`), and the unknown-property refusal ("goal.call has no property module") lists the properties but not the fix. **If I had** the declared type matching the notation, and goal.call explaining its own unknown property ("an argument goes in Parameter={module: …}").
- **educator · `add "<text with %vars%>" to list %x%` keeps the template raw**, where every other action renders it.
- **builder · a note can point at a note the model never sees** (compare.notes pointed at if.notes; fixed, bb92576d0). **If I had** a dangling-reference scan over the notes (a note naming another note's file is a warning, beside MarkdownTeaching's orphan scan), it would surface on its own.
- **os · a method with quoted arguments can't sit inside a quoted text.** `set %path% to "/system/plangos/%file!relative.replace("/shell.next/", "").replace(".staged.txt", "")%"` is refused twice ("%file!relative.replace(…)…% is in the step but not in your answer"): the quotes inside `%…%` likely end the outer text. One method per step, in formal, works. **If I had** a `%…%` inside a text hold quoted arguments, or the builder say "a method with quoted arguments can't sit inside a quoted text: set it apart first".
- **architect · the builder isn't taught text methods.** `%x.replace("\\", "/")%` exists (`text/this.cs:432`, with ToUpper, ToLower, Trim, MaxLength, Grep, GrepCount), but the type catalog lists only context-taking methods (`type/this.cs:736–739`) and no note teaches them, so "replace \ with / in %x%" may not build to it. **If I had** the text methods in the catalog, a plain step would reach them.

## Errors that don't say what to do

- **os · a stale `.pr` says "holds a bool — choice<…> cannot be created from it".** **If I had** the action named and "built by an older builder: rebuild this goal".
- **os · a formal backslash error is a JSON exception** (`'D' is an invalid escapable character… BytePositionInLine: 4`). **If I had** "a backslash in a formal string is written `\\`".
- **educator · an error doesn't show what the step became.** A 4xx error shows the step text and `file:line` only; a 500 shows the parameters, but no template prints the action (`os/system/error/400.txt`, `500.txt`). **Cost:** a learner rewrites a correct step the builder misbuilt. **If I had** the compiled action beside the step ("this step became goal.call(Name=%!app.module.file%)").
- **os · a failed first build leaves a folder that isn't an app**: the next `plang build` says NoAppFound until `--app={"create":true}` again.

## Running

- **os · a path renders with `\` on Windows.** `Combine`/`WithName` build the typed form with `System.IO.Path.Combine` (`path/file/this.Derivation.cs:43`, `PathHelper.cs:28`); `"%file%"` printed `C:\…\shell.next\x`, which broke a deploy. (`%file!relative%` gives the `/` form inside the root, `path/this.cs:146–167`.) **If I had** a path's plang form with `/` everywhere.
- **educator · a permission is granted one file at a time.** `a` persists an exact grant (one path, one verb; `path/this.Authorize.cs:43,58–59`); glob grants exist (`type/item/permission/this.cs:99–114`) but only C# makes them. **Cost:** 22 runs for the course page. **If I had** an answer that grants the folder, or `--allow=read://shared/educator/course` for a run.
- **os · an http error has no response headers** (question 7, with Ingi): a goal can't read a 401's `WWW-Authenticate`; Pull.goal hardcodes Docker Hub's and ghcr's token URLs.

## Testing and gating

- **architect · most plang tests don't run.** `plang --test`: 380 found, 57 run, 323 have no `.pr`. **If I had** a gate step that builds every test goal (or fails on a missing `.pr`), "0 fail" would mean the suite passed.
- **educator · examples aren't built.** No gate or test builds or runs the `*.examples.md` steps (`dev.sh:284–292`), so an example can teach a step that fails. **If I had** each example built and run by the gate.
- **architect · 18 known C# failures in every gate** hide a flake among them. **If I had** them fixed or quarantined.
- **architect · gating a side branch disturbs my tree.** **If I had** the gate run in its own git worktree.
- **architect · stale-binary and path traps in CLAUDE.md** (`./dev.sh test <Class>` skips PlangConsole; `Tests/` is `test/`): proposals v6 and v7, waiting for the docs pass.
- **builder · the binary's `os/` is the source's `os/`** (the same inode): a measurement edit beside the binary changed tracked source on the wrong branch. **If I had** the build copy be a real copy, or read-only, a scratch edit couldn't change source.
- **architect · no automatic secret check.** Today a key-like string count over logs, goldens and `.pr` files is done by hand. **If I had** it in the gate, a leak would stop it.

## Coordination between bots

- **architect · messages without receipts.** A send can fail or time out; the os bot (Remote Control) gives no receipt; a session's name changes on restart. **If I had** a receipt (read, or held), I'd know who has which ruling.

## What works well (keep)

- **os:** "X is in the step but not in your answer" catches real drops; formal lines are an exact escape hatch that survives rebuilds; errors say where (goal, step, line); download Path+Hash (the hash refused a wrong-host page before anything used it); Pull.goal is ~120 lines of plain plang over http, crypto and file.
- **builder:** the per-action notes system is the right shape, and the teaching lever is fast: issue 41 went 0/5 → 5/5 with a one-line addition, measured in minutes.
- **educator:** everyday plain steps build right every time (18 lessons rest on them); errors with `at <file>:<line>` and the step; templates render where written; learner module pages generated from the notes; `--debug={"goal":"X"}` with BEFORE/AFTER variables; `VariableNotFound`'s message says what to do.
