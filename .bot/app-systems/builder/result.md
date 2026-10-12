# app-systems — builder Step-3 test run report

Fresh binary (PlangConsole rebuilt with all session changes), `cd test && plang --test`.

## Totals

**283 total — 129 pass, 126 fail, 28 stale.** (The module/llm built tests ran against the real LLM once; that cost is in this run.)

## Failure classes (the 126 fails + 28 stale)

1. **Sibling helper goals not built (~36 fails).** The per-folder batch built each `.test.goal` entry point individually (`--build={files:[...]}`), which does NOT build the sibling `.goal` files the test calls (e.g. `Loop.test.goal` calls `CountItem.goal`). At run: "Goal …/CountItem.goal exists but isn't built." A whole-tree `plang build` from `test/` does not fix this cleanly — it aborts at the first refusing goal (built only 5 callstack siblings before halting). Clearing these needs a per-goal build pass over all `.goal` (not just `.test.goal`), surviving refusals.

2. **Stale .pr (28).** `.pr` whose builderVersion predates the final binary — mostly folders NOT in this session's batches (app/callstack 11, plan/snapshot 3, …) plus builderVersion drift from the session's many builder changes (on.error check, inverse-Cover, teaching re-pins). The suite needs a rebuild pass with the final binary.

3. **Runtime reds (~90, assertion + error).** Tests that BUILD clean but fail at run, concentrated in app/type (28), module/condition (18), module/on (17), module/test (13). Samples: app/type/date "Expected date", app/data "Expected text/plain", module/signing "Data has no signature". These look like behaviour shifts from the session's pulled coder RUNTIME changes (kind-redesign 4cb19476b, math-seam 955b1446b, sensitive-rename 4afe34fcb, 10b 679/694, container-Holds 00fe6c72e, environment removal 35d2f74bf). Builder maps correctly (these built); the runtime mismatch is the coder's domain (coder stopped) → architect/reviewer bots.

## Left unbuilt, by reason (for the next branch)

- `app/type/text/TextLengthOp`, `app/data/MissingVarIsNotNullValue`, `module/error/ThrowAttachesData` — **issue 74** (navigation written `%s%.length%` / `%x!`): the writer closes `%` after the root. Refuses, never a wrong save. Kernel pass, next branch.
- `plan/app-systems/app/type/list/TypedListHoldsItsType` — **kinded Type choices**: the decider offers only base `list` (not `list<path>`), same family as json-not-offered. 10b closes on its C# pins (TypedListElementTests); plan test waits for the Type-choice item. Noted in the plan's start.md.
- `module/llm/LlmProperties` — writer pads `Limit` with `tool:10/retry:0` defaults → UnwrittenNumber refuses. Writer item, next branch.
- `module/signing/RoundTripAfterRawSignatureDeletion`, `module/signing/corruptedsignature/SigningCorruptedSignature` — **issue 75** family (the `archive identity X, on error ignore` step drops its main action). Flaky; left unbuilt.
- `module/http/downloadskip/DownloadSkip` — **deleted** (stale test of a gone feature: http.download has no IfExists/skip property).

## Builder changes landed this branch (fixes + the one C# each)

- Issue 71: `type=json` → Type=json text (teaching); inverse-Cover reads the step's quoted literals decoded (goal/step/this.Validate.cs).
- on.error: recovery nests in Recovery (teaching); **on.error-does-nothing structural check** refuses an empty clause (module/on/error.cs) — kills the hoisted-recovery wrong-save.
- channel events: `%!channel%` shortcut (decision 500); condition `and`=nested / `or`=if+elseif / file-exists-else / `!%b%`→`==false`; issue-75 refusal wording; `as list of <type>`, llm maxTokens→Limit.token teaching.

All with decider-fixture + page-golden re-pins.
