# codeanalyzer v3 — kind-is-type (render fix + tester F1/F2 pins)

**Scope:** only what landed after my v2 PASS (`560fc0a80`) up to HEAD `d03051109`:
- the coder's **render fix** (`2e83d21ff`, decision 849) — `ui/code/Fluid.cs:165`, the `format is { Name: "item" }` branch (a non-item format renders as plain text; only json/item rests as a structure);
- `d03051109`'s **test pins** for tester v1's F1 (`FileHandlerTests.Read_AsText`) and F2 (`KindFollowUpTests`, new).

**Method:** pulled to `d03051109`, forced build (`rm -f .devsh-stamp*; ./dev.sh build`, exit 0). Read the render fix and both test changes in full; traced the `format` discriminator against the existing kind/type surface; read tester v1 (`.bot/kind-is-type/tester/v1/report.md`), which handed the render name-check to the codeanalyzer deliberately ("for the codeanalyzer's eye").

---

## Verdict: PASS (with one carried finding)

Both changes are behaviour-correct and the two test pins are solid — they assert the real behaviour and go red under the tester's mutations, not the workaround. The render fix works, but it introduces an **"is X? by name" guard** that is a symptom-patch for the text-format birth path — the same root as the already-deferred #3 floor. It does **not** block the merge (the behaviour is correct, and the clean fix needs structural work already routed to branch A), but it is a real finding, recorded, not downgraded.

**Next bot: tester** (to confirm the F1/F2 pins close the false-greens on a fresh build).

---

## F3.v3 — render decides "structure vs plain text" by a format's parent name (symptom-patch)

- **Smell:** *"is X?" by name* (CLAUDE.md, weighted-highest) **+ symptom-patch** (Pass 4.5, tell #4: special-casing by a literal name).
- **File:line:** `PLang/app/module/ui/code/Fluid.cs:165` — `return format is { Name: "item" } ? await format.kind.Decode(…) : Ok(new text(rendered));`
- **What's wrong:** the question the render asks is *"does this format rest as a navigable structure, or is it plain text?"* It answers by the format's **parent name** (`Name == "item"` — json is `{item, json}`, html/md are `{text, …}`). The fix replaced the previous `format != null` (which regressed — decoding an html format did not yield a plain text, 818) by special-casing the one format-parent that works, instead of fixing why a text format's `kind.Decode` misbehaves. The producer (the text-format birth/decode path) hands back inconsistent shapes; the call site learns to recognize the one that's right.
- **Confirmed no clean existing door:** the only kind predicate near this is `IsText`, and json's `IsText => true` (json content *is* text) — so `IsText` can't distinguish json (rests as a structure) from html/md (plain text). There is no "rests as a structure" virtual today, so the name check is a stand-in for one.
- **Root:** the text-format birth path — the **same root as the deferred #3 floor** (`source.cs:115`, branch A's "one make door"); the tester's R mutation (revert to always-decode) regressed `Render_HtmlFile_LandsAPlainText` + `RenderFile`/`RenderInclude` for the same reason.
- **Fix direction:** the format/kind answers *"born as a structure vs plain text"* itself (a virtual on the kind — e.g. the item's kinds say yes, text formats no), so the render calls `format.kind.Decode` for the former and keeps the text for the latter with no name check. That virtual is an **addition to the locked kind surface → a proposal for the architect/Ingi**, and it converges with branch A's one-make-door (born type + kind + format are one shape). 
- **Blocks the merge:** no (behaviour correct, tests green; the clean fix is structurally deferred, as #3 and F8.1 already are). **Surface change:** yes (the clean form adds a kind virtual) — hence a proposal, not a now-edit. **Carry:** to branch A, alongside the #3 floor and F8.1 (the name-check cluster, same root family).

This is not "polish because no callsite is wrong": the behaviour is right, but a name-check guard held in place becomes load-bearing and makes the text-format birth harder to unstick later (Pass 4.5). Recording it so branch A fixes the producer, not another consumer.

---

## The test pins — both correct (F1, F2)

### F1 — `FileHandlerTests.Read_AsText` now asserts before materializing ✓
`d03051109` replaces `IsTypeOf<text>(await result.Value())` (which a `{file, md}` reference's `Value()` also satisfies — the false green M5a) with `result.Type!.Name == "text"` and `result.Peek()?.ToString() == "# hello"` — the **declared type and the unread text face, read before opening**. Distinguishes "landed a text" from "landed a file reference whose Value() would decode to the same content." Goes red under M5a (the Type branch removed); asserts the real landed type, not the workaround. Matches the tester's F1 guidance exactly.

### F2 — `KindFollowUpTests` (new) pins the builder's kind follow-up ✓
Deterministic over `pick.list` with canned answers (as the tester asked, mirroring `OptionQuestionTests`): picks `variable.set`, picks type `number`, asserts the `s0_@kindof.variable.set.Type` follow-up is offered, answers `decimal`, then asserts `Pick.Formal` contains `Type={"name": "number", "kind": "decimal"}` — the kinded type a no-op `Refine` (M8) would leave bare. Pins `KindOf` + `KindsOffered` (incl. the `!k.Internal` filter 814 exists for) + `Refine`; goes red under M8; no LLM, no workaround-assertion. Its `System.IO.*` is test-fixture setup (temp root) — exempt from the production ban. Matches the tester's F2 guidance.

---

## Also noted (not v3 findings)
- `d03051109` and `2e83d21ff` touch no locked-core shape beyond the render call site; the build is clean (0 errors, PLNG001/002 at 0). The v1/v2 shape verdicts stand.
- The tester's OPEN item — the plan's stage-3 strict proof (`SetAsImageGifStrictMismatch`) is red on this branch and on runtime2 — is an architect scoping call, not a codeanalyzer finding; noted for the line.

## Files read
`PLang/app/module/ui/code/Fluid.cs` (render fix, in full context), `PLang.Tests/Modules/App/Modules/file/FileHandlerTests.cs` (F1 diff), `PLang.Tests/Runtime/App/SingularNamespaces/BuilderSchemaTests/KindFollowUpTests.cs` (F2, in full), `type/kind/this.cs` + `type/item/kind/json/this.cs` (the IsText/structure predicate check), `.bot/kind-is-type/tester/v1/report.md`.
