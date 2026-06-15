# v11 — typed-null (.IsNull), Peek non-nullable, dict.Get<T>, OpenAi-via-dict, clr-removal progress

**Branch:** compare-redesign. **HEAD at handoff:** `163003752`. Everything below is
committed + pushed. Working tree clean.

## Read these first (living docs — they hold the real detail)
- `.bot/compare-redesign/coder/clr-removal-epic.md` — the clr-deletion epic: 6 jobs, inventory, decisions, what's done.
- `.bot/compare-redesign/coder/test-failure-clusters.md` — the standing red set clustered by root cause; includes the `grep '^failed '` parse-gotcha.
- `Documentation/Runtime2/native-plang-types-migration.md` — C# classes use native plang types (number/text/list/dict), not CLR. **OpenAi is the pilot.** Holds the `dict.Get<T>(path)` + `.IsNull` patterns.

## Where the numbers stand (per-suite failing counts)
Session moved **Modules 106→49, Runtime 57→48**; Data ~89, Wire 29, Types 13, Generator 7 (those predate this work, mostly untouched).
Build/test: `./dev.sh build` then per-suite `PLang.Tests/<Suite>/bin/Debug/net10.0/PLang.Tests.<Suite> --timeout 120s`. **Data + Runtime SEGFAULT at teardown AFTER printing** — read counts from the log, not the exit code. **Modules truncates** — `Get_SignedPlangResponse_SetsServiceIdentity` and parameterized `OutOfRoot_StreamChannel_*` show as false "NEW" in baseline diffs; use `grep -aE '^failed '` (anchored) for real names.

## Key mental models established this session (don't relearn)
1. **Typed null vs C# null.** A null *value* is the `@null.@this` citizen (a real instance). `item.@this.IsNull` (virtual false; null/type override true) is the test. `Data.Peek()` is now **non-nullable** (`_type ?? @null.Instance`) — absent-ness is `IsInitialized`, null-ness is `.IsNull`. `Peek() != null` (C# ref) was a pervasive bug (caused LLM cache false-hit + `null:443` endpoint). `item.@this.Peek()` is a DIFFERENT `object?` method — leave it.
2. **`dict.Get<T>(path)`** (`app/type/dict/this.cs`) — typed sync path nav (dotted + `[index]`): `dict.Get<number>("usage.prompt_tokens")`. Use it to read structured values; no `JsonElement`.
3. **All I/O through the channel** — http body serializes via `Serializers.GetOrDefault(ct).SerializeAsync`; responses navigate the channel-deserialized dict. No raw `JsonSerializer.Serialize(value)` on items (it bypasses converters → `{Cacheable,Prior,IsLeaf}` leak).
4. **clr removal** (Ingi: it's a hack, remove it). Providers + Guid/DateTime done. Remaining jobs in the epic doc.

## Next steps (in priority order)
1. **OpenAi numeric chain → `number`** — finish the pilot class: token counters (`int`), cost math + pricing table (`decimal`) still CLR. `number` has full operators; `number`/`text` aliases already at file top.
2. **2 remaining LLM:** `Query_ToolParams_DefaultValueMeansOptional`, `Query_ToolParams_NullValueMeansRequired`.
3. **clr-removal jobs** (epic doc): #2 stamped raw containers → narrow dict/list; #4 declared-label re-wrap of existing items; #5 `archive : item` for the compress courier; #1 domain tail (`goal`, etc.) → `:item`; then delete fallback + `SetValueDirect` + `Lower<T>` + clr class.
4. **Standing red set** (Data ~89, Runtime ~48) — cluster doc has the breakdown; many are pre-existing, unrelated to this arc.

## Workflow reminders (from this session)
- Ingi steers closely and wants the PROPER fix, not shortcuts ("we're making a programming language"). Show the problem + proposed solution BEFORE editing; he often refines the design mid-stream.
- Lead high-level (design), not file paths/line numbers.
- `./dev.sh build` rebuilds `PLangLibrary.dll` even on internal-only edits (verified) — but if a probe doesn't fire, check the dll timestamp before assuming stale.
- Mutation/instrumentation probes: announce, revert, keep `git status` clean.
- Commit + push after each clean unit (Ingi: next bot reviews origin).
