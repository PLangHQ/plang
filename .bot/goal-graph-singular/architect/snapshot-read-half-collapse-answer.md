# architect → coder — a section IS an entry whose value is a snapshot: collapse approved

Answers `to-architect-snapshot-read-half.md` (`d0dead868`). Ruled by architect 2026-09-22 with Ingi afk; Ingi reviews on return.

> **You own this.** Ruling settled; mechanics yours.

## The collapse — approved, and it is the one-structure law, not a reader trick

The blocker is real: entries write self-describing Data, sections write bare objects, and on the wire nothing marks which is which — a reader could only sniff shapes, i.e. re-grow the type-switch deleted two commits ago. The cause is the asymmetry, so the fix is the collapse: **a snapshot has ONE structure — a dict of Data — and a section is an entry whose value is a snapshot.** `Entries` is the one bag; `Output` is one loop; `Sections` / `HasSection` / `SectionNames` become views over the entries holding a snapshot; `Section(name)` is get-or-add of such an entry. `_sections` dies — it was stored-twice: a second home for part of the same tree, distinguished only by which method put it there.

Timing: agreed it is free NOW — restore throws, the file's own comment says a snapshot is in-process state replayed into the same actor, no external consumer is pinned to the layout. After the read half lands, changing the shape means doing both halves twice.

## Unknown (1) — name the type explicitly

Yes, prerequisite: `snapshot.@this` overrides `Type => new("snapshot", typeof(@this))` like `goal.call` does. A nested section rides as `{name, type:{name:"snapshot"}, value:{…}}`, and the data reader dispatches on that tag — reflection-derived naming is not something a wire should depend on.

## Unknown (2) — a registered `ITypeReader`; `Create` becomes the normal courier

The law: values materialize through the registry; program structure is constructed by its parents. A snapshot is a **value** — it rides the wire as a Data value, `Data.Value<snapshot>` dispatches to it — and a section needs no parent birth fact (nothing in the snapshot's surface references a parent). So:

- `snapshot/serializer/Reader.cs` — registered `ITypeReader` (parameterless ctor). Reads the dict of Data rows; each row reads through the data reader, which dispatches a snapshot-typed value back to this reader — recursion falls out of the registry, no walker.
- Born with the READING context (`ReadContext.Context`) — correct for a run value: a restored snapshot belongs to the actor restoring it. (Not the program-structure carve-out; a snapshot is never shared structure.)
- `Create` at `this.Wire.cs:46` stops throwing and becomes the ordinary ICreate courier: pass-through when already a snapshot, decline otherwise. The wire read happens at the serializer boundary, not in `Create`.
- Root stays bare (no envelope, unsigned — `Serialize` as today); nested sections ride as envelopes. The root reader reads a dict-shaped object of Data rows.

## Scope

As you drew it: Resume, the restore dispatch ladder, presence-as-signal for build/test, and the two CLR boundaries stay as todos #3. Fix the stale `this.Wire.cs:28-30` comment (it still names `serializer.Default`).

## Verify

1. Capture → Serialize → read back → `Sections`/`HasSection` views answer the same as before the collapse (the views are the compatibility surface for every ISnapshot section).
2. A nested section round-trips typed (`Value<snapshot>()` pass-through after read).
3. Wire's deferred-read reds turn green; diff by name.

## LANDED (`e5865c985`) — read half open on value fidelity

Structure as ruled. Coder's find worth recording: a structured wire payload rides as `wire.@this` (born holding the serializer that sliced it), never as a bare `source` (whose `Read()` decodes one scalar token and yields the null citizen). `SnapshotFromWire` now slices with a plang serializer, the mirror of `Serialize`.

**Open:** a captured variable comes back null (`Variables_SurviveWireRoundTrip`). Design expectation stated to coder: rows keep their envelope end to end — `Data.Output` writes `name` in Store view (`data/this.Output.cs:86-90`), the list reader reads each element through `ReadSlot` → `IsTypedEntry`/`@schema:data` → the data reader → a named Data row (`list.AddRaw` keeps it). So a null is one hop breaking the contract; coder instruments four hops (serialized string; the entry's slice + Type; rows after `Value<list>`; `Restore`'s `Set(data.Name, …)`) and reports. Ruling on the fix waits for evidence — no guessing at the list/Data boundary.

**RESOLVED by evidence (hop 2b):** the write was perfect (names, envelopes, no flattening). A read section arrives as a typed `snapshot` WIRE SLICE (the data reader's lazy value arm), and the views test `Entries.Get(name)?.Peek() is @this` → false → `HasSection` false everywhere → `App.Restore` visits nothing. Not value fidelity — the sections were never visited.

**Ruling: (c) — the snapshot reads EAGERLY, and eagerness is the TYPE's declaration.** Coder dismissed (c) as unavailable ("materializing is async") — but materializing a typed slot off the same stream through its pull reader is a sync ref-struct read; the data reader already does it for goal.call (`data/reader/this.cs:69-74`). Rejected: (a) two doors (fork); (b) making every `Capture` async to compensate for reading structure as if it were lazy content. Not as `|| typeRef.Name == "snapshot"` — that string switch is a type-switch in a courier and this bug is its second customer. Instead:

```csharp
// ITypeReader — default interface member; only the two eager readers override:
bool IsEager => false;                 // "my values are structure: read me off the stream, never slice me"
// goal.call Reader, snapshot Reader:  public bool IsEager => true;
// data/reader/this.cs value arm — replaces the goal.call name check:
if (typeRef is { IsNull: false } && ctx.Context.App.Type.Reader.Typed(typeRef.Name, null) is { IsEager: true } eager)
    value = eager.Read(ref reader, null, ctx);
else … the lazy slice arms as today
```

One rule, two declarers, the data reader stays generic; lazy stays the default (store-raw-type-on-read is deliberate; eagerness is not widened beyond the two). A read snapshot then holds real snapshot instances, `Peek` stays honest, the views stay sync, `Capture` stays sync. Test: read a snapshot back; every section answers `HasSection` true and `Value<snapshot>()` passes through.

**IsEager landed (`6c6a6a535`)** — Wire 470/27 vs the 29 baseline; the two snapshot round-trip tests pass. Remaining reds stop in Providers: `Registration`/`DefaultOverride` are positional records the reflection read cannot construct ("no parameterless constructor"). Coder tried the cheap unblock (a parameterless ctor) and REVERTED it — correctly: it births an invalid record (born-valid inverted) and turns a loud construction failure into a blank type name restored as data.

**Ruling: the two records become plang value types by the value-type recipe** — item + ICreate + `[Out]` props (already tagged) + their own `serializer/Reader.cs` reading `{typeName, providerName, source}`. Not settable records read by reflection (keeps the reflection kind as the door, needs the invalid ctor anyway, makes `init` props mutable — the late-stamp shape). Registration: if the code-module-local namespace does not fit discovery's `app.type.<name>.serializer` shape, register explicitly as goal.call does (`reader/this.cs:172-175`). `IsEager` stays false for them — `Restore` reaches them through `Value<registration>()`; eagerness is only for structure the graph walks synchronously.

**Landed (`947c973c4`) — Wire 470/24 vs the 29 baseline, FIVE fixed incl. the original null and every full-app round trip (Providers is reached by all of them).** `registration`/`defaultoverride` are items under the code module with ICreate, `[Out]`, and their own readers. Two corrections to my notes: discovery keys on ANY `*.serializer` namespace (`reader/this.cs:186-197`) — no explicit registration needed, only `[PlangType("registration")]` for the catalog; and `Rows<T>` now reads through `Value<T>`, so the "two genuinely CLR sites" exception for these records is gone, not moved. `IsEager` false on both.

**Next (ruled with Ingi afk):** (1) classify the 24 remaining Wire reds by NAME into buckets — deferred read-half remainder (todos #3) / CLR-boundary descendants / flaky / real regression — read, don't fix; (2) the flaky-test pass before further feature work: every flaky test is FIXED at its root or QUARANTINED with the cause named, never left drifting (Types now drifts 27–30; `Formats_ExtensionToPlangName_ReadsThroughRegistry` is the first candidate); (3) Ingi's review before Step D/E.

**The 24 classified (`80c27ddc9`, coder's `wire-reds-classified.md`):** all in the intra-branch baseline (nothing from the snapshot work). (a) EIGHT = the deferred restore remainder (todos #3 — one structural pass, incl. presence-as-signal); (b) ONE = Statics' own untyped bag; (c) the flaky PathKind pair (absent this run — count comparison is worthless); (d) my prediction landed — `Deserialize_ShallowNesting_StillWorks` self-nests `{"name":"a","value":{"name":"a",…}}` = the `data.@this<T>` double-wrap footgun, now visible (correction: it is a DESERIALIZE test, so my "look at `Clone()` at the capture site" pointer does not apply — look where a Data lands in a `Data<object>` slot on the read path); a test-path bug (`SignatureType_XmlDoc…` reads a moved path — fix as a test bug); and **three security-shaped failures failing OPEN**: two tamper-detection tests ("expected failure but Data succeeded") and a sensitive value leaking into an assertion message.

**Ruled (Ingi afk): the security three jump the queue — provenance first.** The 29-name baseline is intra-branch; it says nothing about `runtime2`. Run the three against `origin/runtime2` in a worktree. Green there → THIS BRANCH broke them → top regression; suspects: the `Verify: false` we put on nested/ingest reads (kind bridge, data reader arms) if an outer verify is now treated as nested, and the error-render changes around `app.Error`'s deletion for the masking test. Red there too → base debt, fixed before merge, recorded in open-items. Then: double-wrap → test-path fix → flaky pass → restore/todos #3 → Statics.

**PROVENANCE RESULT (`b31603cad`): all three security tests PASS on `origin/runtime2` (0ea5a4b94) and FAIL here — branch regressions, failing open, merge-blocking.** Coder's own correction, adopted as a rule: a baseline is against the branch BASE (`origin/runtime2`); an intra-branch baseline shows only what one pass changed and must never be read as "pre-existing".

**Ruled: bisect, don't hypothesize.** `git bisect start HEAD $(git merge-base origin/runtime2 HEAD)` with the three tests as the oracle (each its own oracle if one commit explains only some). Culprit commit(s) recorded before any code is touched. Then fix at ROOT: if the culprit is the `Verify: false` on nested/ingest reads, the fix is NOT flipping a default — `ReadContext.cs:23-25` states the invariant (the OUTER transport read verifies; a NESTED reconstruction is covered by the outer signature) and that decision belongs to the caller that knows its position; a bridge serving both must be told. If it is the error-render change (masking test), the `[Masked]`/`[Sensitive]` discipline lives on the value's own output face — route the assertion message back through it, never re-mask at the message site. The three test names join this branch's verify gates. Reference worktree `/tmp/rt2` kept.

**Smell found on the trace (todo, not the bug today):** the data reader mints a nested slice with `ctx.Context.Actor?.Channel.Serializers?.Transport` (`data/reader/this.cs:102`) — the actor's transport, an ambient reach — instead of the serializer actually reading (the capture handing itself, as `wire.@this`'s doc requires). Always the plang serializer today (`serializer/list/this.cs:138`), so harmless now; cursor-as-identity in shape. Fix when touched: the reader that slices carries its serializer and hands it to the wire it mints.

**BISECT RESULT (`d965eaeeb`): my suspect is DISPROVED; TWO independent regressions in disjoint ranges.** Measured at `da067599c^` (the commit before "Stage 4: no-verify flag for nested reconstruction"): both tamper tests already FAIL there — so the no-verify commit did not cause them; their range is `0ea5a4b94..da067599c^` (773 commits). The masking test still PASSES there — its range is `da067599c^..HEAD` (963 commits). Blind `git bisect run` is unreliable on this branch and fails silently as "skip": the test layout switches mid-branch (one `PLang.Tests` project at the base, six suites later — the oracle must detect which exists), and a stale `obj/` from the other layout makes msbuild emit NOTHING while reporting success; cleaning `PLang/bin` alone then breaks the test projects ("could not copy PLangLibrary.dll"). Four silent skips before coder spotted it. What found it in two builds: `git log <base>..HEAD -- <paths>` to a candidate list, then candidate-vs-parent measured in `/tmp/rt2`.

**Ruled (Ingi afk):** tamper pair FIRST (fails open on verify; the older range; both flipped in the same range → one oracle until they diverge), masking second by the same procedure. **Narrow-then-test is the procedure; bisect-run only closes gaps.** Three rules: (1) the narrowing paths include the TEST files themselves incl. their pre-move location — a flip coinciding with a test rewrite is reported separately before continuing (it changes what "regression" means); (2) narrow on BOTH ways a tampered value can verify — verify skipped (ReadContext/Verify paths, the kind bridge, the reader arms) OR properties outside the signed bytes (the signed payload's construction: canonical form, Output's Store view, the wire writer); (3) binary-search the candidate list; a culprit is confirmed only by parent-passes/commit-fails, and if the first failing candidate's parent also fails the culprit is a non-candidate in the gap — close it with the oracle. The oracle script is committed under `.bot/goal-graph-singular/coder/` (scratchpad is ephemeral) and starts with the full bin/obj wipe from the CLAUDE.md stale-binary recipe. Culprit + its diff in the doc before any code; the fix ruling reads the diff.
