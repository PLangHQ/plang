# Builder handoff — stage 5 (the builder offers kinds) — PAUSED

Paused mid-stage-5 at Ingi's request (save usage). Resume only on Ingi's or the architect's word.

## Where things are

**Committed + pushed:** the stage-5 MECHANISM — `0fda873ec` (rebased onto the coder's re-pin `3f4f569bc`).
- `pick/question/this.cs`: `Kind.KindOf` + `Of` field.
- `pick/list/this.cs`: a kind follow-up — `Questions()` adds a KindOf question when the stage-2 Type pick is a
  kind-capable type; `KindsOffered` (a generic offers the registry's types as its element, else `type.kind.list` less
  Internal); `Refine` folds the answer into `{name, kind}`; `Picked` resolves the pick.
- `Decide.goal`: a conditional **stage 3** (`if %questions% contains "@kindof"` → llm.decider + build.pick).
- `decider3.template` (new); `decide.pr` rebuilt from the **os/ app root**.

**Uncommitted (in the working tree — left per the pause instruction):** the TEACHING + the re-pin, NOT yet committed.
- `os/system/modules/variable/set.notes.md` — the Type `ask:` now steers `as json` → item (kind json follows), dict only
  when the step says dict/map.
- `os/system/builder/llm/templates/decider3.template` — the scalar follow-up wording made GENERIC (no type/kind names):
  "a word beside the type word — one of the kinds offered here — names that kind; the type word and the kind word can
  sit together, in either order."
- `os/system/type/item/type.description.md` (new), `os/system/type/{list,duration,dict}/type.description.md` — each now
  explains its kinds (writer/type-page teaching; these do NOT drive the decider pick — the question wording does).
- The six `test/plan/kind-is-type/.../*.test.pr` rebuilt during measurement.

## Results so far (5 builds each, fresh cache:skip, jq) — 4 of 5 + guards

| sentence | result | |
|---|---|---|
| `as decimal` | `{number, kind: decimal}` 5/5 | ✓ |
| `as list of path` | `{list, kind: path}` 5/5 | ✓ (baseline was 2/3 flaky; `[0]` born path) |
| `as json` | `{item, kind: json}` 5/5 | ✓ (after the set.notes ask steer) |
| `as iso duration` | `{duration, kind: iso}` 5/5 | ✓ (after the generic decider3 wording) |
| `set %n% = 5` / `as text` | number / text | ✓ guards |
| `read 'notes.md' as text` | `{file, kind: md}` 5/5 | ✗ — see below |

`plang --test` on `test/plan/kind-is-type/`: 15/17. Fails: `AsTextKeepsText` (below) and `app/type/json/PassedOnUnread`
(a stage-4 json test, the coder's, not stage 5).

**Extra-exchange count:** stage 3 fires ONLY for a goal with a step coercing to a kind-capable type; a no-coercion goal
renders `{}` and the `if` guard skips the exchange entirely (measured: 0 KindOf ids → no stage-3 call). The common case
pays nothing.

## Next step (resume here)

1. **The re-pin (the immediate next action).** PickListTests moved — 3 fail: `StageOnesAnswer_*RendersThePinnedStageTwoQuestions`
   (×2) and `ThePromptCUserMessage_IsThePinnedOne`. That's my stage-5 delta (the Type ask wording + stage-3 KindOf
   questions on coercing golden goals + item in prompt C). Re-pin via `./dev.sh test PickListTests` then its Explicit
   `AcceptTheFixture`, **reading the pick_golden.json diff against 3f4f569bc** (it must be only the stage-5 delta, not the
   coder's already-pinned drift). Also run and re-pin as needed: PickOptionTests (in-code; the offered type NAMES are
   unchanged, so expected green), ConfirmTemplateTests, LineTwinTests, and TypePageTests (the list/duration/dict/item
   descriptions moved the type page — `Documentation/v0.2/type-reference-generation.md`). Diffs read before accepting.
2. **Commit** the teaching + the re-pinned goldens + the rebuilt test `.pr` together (teaching-edit rule: one commit).
3. **`read 'notes.md' as text` → text needs C#, not teaching.** `file.read` (`PLang/app/module/file/read.cs`) has only
   Path + Template — no Type/coercion slot — and a capture (`write to %t%`) isn't asked its Type (`pick/list` guard at
   `:587`). So `as text` can't be captured by teaching. Route to the coder (via the architect): a coercion/Type slot on
   `file.read`, or allow a capture's Type when the step writes `as <type>`. Flagged to the architect already.

## Build notes learned

- Rebuild `decide.pr` from the **os/ app root** (`cd os && plang '--build={...,"files":["system/builder/BuildGoal/Decide.goal"]}'`),
  NOT the project root (which contaminates goal paths with the bin absolute path). `os/` needs the decider key:
  `cp .data/setting/data.sqlite os/.data/setting/data.sqlite` (gitignored).
- `dev.sh build` hardlinks `os/system`→bin, so os/system source edits (and a rebuilt `.pr`) are live in bin.
- The decider is taught by the QUESTION it is asked (criteria + ask wording), NOT by type descriptions (those feed the
  writer/prompt C). That's why the base-pick fix had to go in the set.notes ask and the decider3 wording.
