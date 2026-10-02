# coder — friction

One entry each: what, what it cost (evidence), the wish.

## A handler fault inside the http test server is swallowed
- **What:** an exception thrown in `HttpTestServer`'s handler came back as a bare 500, or as a hang, with nothing in the test log saying the handler threw.
- **Cost:** a flaky download test took several reruns to tell a real fault from a timing one. I added `HttpTestServer.Faults` and a "faulted" log line only after that.
- **Wish:** a fault in any test fixture's handler fails the test that caused it, naming the exception.

## The Gate binary and the Debug binary disagree
- **What:** `./dev.sh full` builds the Gate config. A class run afterwards (`./dev.sh test X`) rebuilds only that suite's project in Debug. Others are left stale, so a later run loads two PLang assemblies: "Value is of type this, not this".
- **Cost:** on 2026-10-02 the size gate showed 19 C# failures. Telling them apart from mine took a stash, a rebuild and 7 class reruns (~10 min). An explicit-test re-pin (`AcceptTheFixture`) also needs a manual rebuild first.
- **Wish:** dev.sh marks which config the bin was built with and rebuilds when it changes. Also, the 18 known failures listed by name in one file that dev.sh diffs against: "18 known, 0 new" instead of a red wall.

## A re-pin diff can print the LLM key
- **What:** settings_golden re-pins rendered `setting.kind` rows with the env `OPENAI_API_KEY` as a default. Reviewing the fixture diff would have printed the key.
- **Cost:** I reverted the golden, pinned sensitive-options-have-no-default, and since then I check fixture diffs by count only (`grep -c sk-`), never by reading them.
- **Wish:** fixtures are scrubbed on write: any value that equals an env secret becomes `***`, and the test fails loudly if one is found.

## The pick golden fails as one 1,800-line string
- **What:** `StageOnesAnswer_RendersThePinnedStageTwoQuestions` asserts one joined string. TUnit says "content too large for detailed diff", and dev.sh's log truncates it.
- **Cost:** I had to edit the test temporarily to dump the diff to a file, then script a JSON compare. That compare showed all 20 diffs were just the new `size` type option.
- **Wish:** the golden tests write their diff to a known file (e.g. `/tmp/golden_<test>.diff`) and assert a one-line summary: "20 questions differ: options added [size]".

## Every new plang type joins every `as <type>` offer
- **What:** the builder's `as <type>` choice for `variable.set` lists every plang type. `size` and then `progress` each added 20 lines to pick_golden. `progress` is a report a module hands a goal; no one coerces a value to it.
- **Cost:** each new type means a re-pin, plus a scripted check that the re-pin added only that option. The offer list in every set step also grows longer, which is noise for the model.
- **Wish:** a type says whether a step can name it (like `Internal`). Module report types (progress, redirect?) stay out of the coercion offers.

## PLNG002 flags a qualified `System.IO.IOException` in a catch filter
- **What:** `catch (Exception ex) when (ex is System.IO.IOException …)` fails the build with PLNG002 "bypasses FilePath.AuthGate". The unqualified `IOException`, via implicit usings, passes.
- **Cost:** one failed build. The fix is to spell it unqualified, which is evading the check rather than honoring it.
- **Wish:** PLNG002 looks only at members that touch the disk (File, Directory, FileInfo, Path.Combine…), not at exception types named in a catch.

## Two closed sets with one plang name clash silently
- **What:** I gave the archive's compression-level enum `[PlangType("level")]`. The debug module's `Level` enum already had that name. Nothing complained at load: `debug.setting.level = "action"` just stopped resolving, and a debug smoke test failed far from the cause.
- **Cost:** a full gate plus a class rerun to trace it back to a name I had picked an hour earlier.
- **Wish:** the type registry refuses a second type or closed set with a name already taken (as it already does for a MIME type or extension, TypeLoadCollision), naming both classes.

## `--build={"files": "x.goal"}` refuses a single file
- **What:** `plang '--build={"files":"serialization/CompressRoundTrip.test.goal"}'` fails with "cannot lower a String into list: the target owns no Clr projection for this shape". It needs `["…"]`.
- **Cost:** one failed build, and the message speaks C# (Clr projection, lower door), not CLI.
- **Wish:** a lone value given where a list is wanted is a list of one. Failing that, the error says "files is a list: --build={\"files\":[\"x.goal\"]}".

## 323 plang tests are "stale: no .pr" in every run
- **What:** `plang --test` reports 323 of 380 as stale because they have never been built, and ends in `TestRunFailed`.
- **Cost:** every gate ends red, so a real stale test (a goal changed but not rebuilt) is invisible among the 323.
- **Wish:** a never-built test is its own status ("unbuilt"), counted apart from a stale one, and doesn't fail the run. Or the tree gets built once.
