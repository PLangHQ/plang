# Handoff — variable-as-value: unblock `plang build`

## THE GOAL (don't lose it again)
This branch exists to **make `plang build` work**. Done-state = `plang build` runs +
`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green. NOT "green C# unit
slices" (that's a side-effect). Plan: `.bot/variable-as-value/coder/plan.md`.

---

## STATUS (updated 2026-07-08 — after merging `clr-navigators`)

### ✅ Done and merged
- **variable-as-value core** — a full-match `%x%` borns a first-class `variable` (not text),
  never parsed at load; resolves at `.Value()`. The `%msg%` self-reference blocker fixed.
- **`clr-navigators` MERGED back into this branch** (merge `936b5fdf9`; branch deleted). The
  kind machinery: a value navigates/converts/serializes by its **kind** without materializing;
  a `clr` navigates by its kind (not reflection); `json` stays a navigable `clr(json)` end to
  end; `type.@this.Kind` is a first-class `kind.@this`. Full C# suite at baseline. Scan +
  status: `.bot/clr-navigators/coder/{stages,obp-scan,baseline}.md`.

### ⚠️ The builder is NOT green yet — two blockers remain
Rebuild the console (`dotnet build PlangConsole`) and repro from `Tests/`:
```bash
cd Tests
../PlangConsole/bin/Debug/net10.0/plang '--build={"files":["BuilderSanity/BuilderSanity.test.goal","BuilderSanity/AddItem.goal","BuilderSanity/MarkBig.goal","BuilderSanity/Finalize.goal"],"cache":false}'
```

**Blocker 1 (START HERE) — `%plan%` is not RELIABLY a navigable `clr(json)`.**
`foreach %plan.steps%` still hits `IndexNotSet` (`%planStep% holds list = '[Start'` — the
plan string is being char-iterated, so `%plan.steps%` isn't the array). It's
**non-deterministic** (LLM plan varies): the machinery navigates *when* `%plan%` is a
`clr(json)`, but the wire read of the plan — declared `object` on the `.pr` — still borns
`source(object, text/plain)` on some paths. The fix is the one piece I deliberately deferred
during clr-navigators:
- **`data/reader/this.cs:79-80`** — the deferred-value read picks the format by TOKEN SHAPE
  (String→text/plain) regardless of declared type. Route an `object`/`dict`/`list`-declared
  (or json-kind) wire value → json → `clr(json)`. **SENSITIVE:** this is the `variable-as-value`
  line — must NOT turn a full-match `%var%` into a clr (a `%var%` borns a `variable` in
  `type.Build`, `type/this.cs:265`, a different branch; verify it stays that way).
  Architect's demolition audit `[replace]` #5: `.bot/clr-navigators/architect/demolition.md`.

**Blocker 2 (after 1) — `goal.getTypes` List-lower.**
When `%plan%` DOES navigate (the lucky runs), the build advances to
`BuildStep/Start.goal:19` (Compile) and dies:
```
goal.getTypes: InvalidCastException: List`1 cannot lower to 'this' — the type must own this Clr projection
```
`goal.getTypes` returns a typed `Data<list<dict>>`; a native `List` is lowered to the
`list<dict>` target and `item.Clr` (`type/item/this.cs:364`) rejects it. Not yet root-caused —
could be the typed-return wrapping, or the demolition audit's #10 (`build/Default.cs` dual-path
step readers reaching for a raw `JsonElement`/`dict` where steps are `clr(json)` now).

### Deferred (own branches, not builder-blocking)
`identifiers → text`, `Peek → item.@this` (architect's deferral list). The remaining C# slice
failures are the `data.Output` write-path tail (output-redesign.md), separate from the builder.

---

## PROCESS (Ingi's corrections — follow these)
- On a plang/builder failure: **improve the error message first** (self-diagnosing, keepable)
  and **use `plang --debug={...}`** — NOT `Console.Error` dumps in C#.
  (memory: feedback_debug_plang_failures).
- Show the design before changing C# on a sensitive line (the `data/reader` line is one).
- Commit green chunks; push (pipeline reviews origin).

## Build/test
`./dev.sh full` (all C# slices, at baseline), `dotnet build PlangConsole` (the plang exe),
then the `--build` repro above.
