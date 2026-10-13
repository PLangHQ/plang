# codeanalyzer — app-systems

**Version:** v1

## What this is

The pre-merge OBP scan of `app-systems` before it merges back to `runtime2` (Ingi decision 721; the review the architect's brief `.bot/app-systems/architect/briefs/codeanalyzer.md` assigned). No tester/security/auditor/OBP pass has run on this line since runtime2 (`0ea5a4b94`, 2026-06-05); the diff is 977 C# files. The job: find shape mistakes in the locked core (where they cost most — the surface can't change without Ingi) and the new builder pipeline, before the merge carries them into runtime2.

## What was done

Scanned in 9 sections (one per brief area, resumable; `v1/report.md` has them in full), each `git diff 0ea5a4b94..app-systems` + full read of every touched type + `ObpScan` + the carry-list check (`next-branch.md`, `obp-cleanup.md`, the decision log). Committed and messaged the architect after each.

**Verdict: PASS — merge-clean on behaviour. No finding blocks the merge.**

- **Areas 1 (data+wire), 2 (channel+stream), 3 (core value types), 4 (type system), 5/5b (serializer + kind/{dict,list,reflection}), 6 (call/variable/events), 7 (module+app):** the new surface is OBP-exemplary — registries with behaviour on the element (schema reader, Operator, path scheme, module actions, the `on/` event family), single owners for cross-type relations (number Ladder, Comparison), `app.@this` a clean composition root. The weighted smells present are almost all already on the carry list (673 reflection, 714 reader-throws, 717 json clr-leak, #59 set-child bridge, 677 pick-by-name, 681 Sensitive, 711 kind-is-type) — cited, not re-raised.
- **Area 8 (builder pipeline):** the carried Cover↔Freeze stored-twice is fixed; the pick-list name forks are the carried 677/714 cluster.

### Findings (none blocking; all next-branch cleanup / surface proposals)

| ID | What | Surface change | Fix direction |
|----|------|----------------|---------------|
| **F5.2** | **Two write doors** — self-writing `Output` vs the sync `IWriter.Value(object)` type-switch; dict/list wire shape written in several places. The heaviest shape debt the merge carries (on next-branch via 724). | yes (locked) | one door — everything through `Output`; retire `Value(object)`/sync `Write` |
| F4.1 | `IsDefault` (`type/property/this.cs:118-138`) forks default-equality by type-name | yes (IsDefault sig) | value answers via `Compare(Type.Make(Default))` |
| F8.1 | the "action slot" predicate re-derived in 4 sites / 3 files (carried with 711) | no | the value reads its own — no site asks the type what it is |
| F3.1 | parse-vs-convert failure face duplicated across 9 value types | no | the source value answers its own failure face |
| F5.1 | stale `Normalize` doc in 3 places (`IWriter.cs`, `json/writer.cs:200`, `Tagged.cs:8`) | no | update docs to the self-writing `Output` path |
| F6.1 | system/user-variable predicate repeated (~10 sites) | no | carried; architect reads the two subsets as intentional |
| F1.1 | dead const `MaxNormalizeDepth` (`data/this.Output.cs:21`) | no | delete |

## Code example — the one that matters (F5.2)

```csharp
// json/writer.cs Value(object) — a second write door that type-switches…
case app.type.item.dict.@this dict:   // …even though dict writes itself (dict/this.cs:175 Output)
    _writer.WriteStartObject();
    foreach (var key in dict.KeyNames) { _writer.WritePropertyName(key); Value(dict.Stored(key) is app.data.@this d ? d.Peek() : dict.Stored(key)); }
    _writer.WriteEndObject(); return;
// …its own comment two lines down: "the writer never type-switches per type (OBP Rule 9)"
case app.type.item.@this v: v.Write(this); return;   // the general self-write door
```
The live serialize path is each value's async `Output`; `Value(object)` + sync `Write` is, in the code's own words (`item/this.cs:917`), "the OLD Peek().Write path."

## Environment

Ran in an isolated worktree `/workspace/plang-app-systems` on `app-systems` — the shared `/workspace/plang` checkout (on `compare-redesign`, ~14 live sessions) must not have its branch switched. Each commit rebased onto the architect's pushes before push. Session `plang-ae`.

## Next

Architect verified every area against the code as it landed (`.bot/app-systems/architect/review-findings.md`); F5.2, F4.1, F8.1, F2.3 are batched to Ingi as surface asks. Nothing routes to the coder as a merge blocker.
