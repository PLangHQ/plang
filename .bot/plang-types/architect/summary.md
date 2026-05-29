## 2026-05-29 — Consolidation pass: clean plan docs for read-over

All review comments resolved — the design is settled. Rewrote the plan documents to read forward as a settled design, stripping the process scaffolding (dates, "Ingi flagged", "Opus 4.8 point N", "earlier draft / withdrawn", "pending / open question" where the call is made).

- **plan.md** — full rewrite. Spine reads clean: Why → the three commitments → the movie → precedents → vocabulary (3 proving instances + 4 cleanups + a "deferred, by design" list folding in BigInteger/rational/quantity/video-etc.) → runtime extension → folder tree → cross-cutting decisions → a 6-item **stage index** (to carve next). Dropped the "settled in review / open questions / how this lands vs old plan" process sections.
- **The divide call is made** (was the last dangling open item): `/` and `^` leave the integer track (`7/2 → 3.5`), truncating division is the explicit `math.intdiv`. Written into storage.md as the design + a one-line architect's-call note in the spine so Ingi can veto on read-over. The integer-division footgun is the wrong default for a non-programmer audience.
- **storage.md** — struct rationale stated cleanly (value semantics; the boxing caveat kept as a coder note, not a correction narrative). Divide/Power section replaces the "pending" block. Equality keeps the honest non-transitivity caveat (that's design, not process).
- **policy.md** — `app.config` design stated directly; handler example aligned with the Data-returning error model.
- **types.md** — serializer-file shape throughout; struct/no-Context for number; discovery-time + runtime registration.
- **dispatch.md** — header + "what changed from first draft" narration trimmed to a one-line rationale.
- **Removed `plan/review-opus-4-8.md`** at Ingi's request — not useful for the coder; its decisions are folded into the main docs.

Net: five plan docs (plan.md + storage/policy/types/dispatch), self-contained, no cross-references to process history. Next step after read-over: carve the six stage files.

## 2026-05-29 — Dispatch signed off, struct landed, runtime type-loading captured

Ingi signed off on the per-(type, format) serializer-file dispatch and made the struct/rational calls. Five open comments addressed.

**Dispatch — rewrote `plan/dispatch.md` wholesale.** The `IWireWritable`-single-method-with-mime-switch design is gone. Settled shape: `app/types/<name>/serializer/<format>.cs`, one `Default.cs` (uniform rendering) plus a file per format that genuinely differs (image needs `text`, `protobuf`, `Default`; number/code/path need only `Default`). The source generator emits a `(typeName, formatToken) → Write` table; the writer carries a `Format` token and does the lookup; `Normalize` tags registered-type values as a deferred `TypedValueNode` and the writer resolves it. This collapses the "~75 tiny files" worry — most types are one file. Grounded the doc on real surfaces verified this session: `Registry.cs` (`[PlangType]`, `RegisterRuntime`, `ResolveType` runtime-precedence) and `code.load` already exist.

**Runtime type-loading captured (new spine section).** Ingi's L3 comment: `- load mynumbers.dll` should detect the interface, inject the type, allow overwriting `int`. This is already mostly wired — `code.load` is the load-scan-register template, `RegisterRuntime` is the hook, `ResolveType` already favors runtime over built-in (so overwrite works at the resolution layer). Honest limit documented: runtime registration changes resolution + rendering, not what the generator already baked (PLNG slots, compiled `Data<int>` params, shipped `.pr` stamps).

**`number` is now a `readonly struct`** (Ingi's call). Named `@this` so the convention is intact. **Architect correction surfaced:** verified `Data.Value` is `object` (`app/data/this.cs:86`), so a struct boxes on store into Data — the allocation win Ingi cited holds only for pure-C# reducer accumulators, not the dominant Data path. Struct still stands on value-semantics grounds; flip to `sealed class` is one keyword if the corrected premise changes the call. Folded in the already-agreed equality (lenient default + `ExactEquals`, non-transitivity caveat documented), Context removal, and error model (throws at C# boundary / Data at handler boundary) into `storage.md`.

**Rational numbers** (L191): Option B (separate `rational` sibling type), deferred — not this branch. Recorded in `review-opus-4-8.md` + plan.md open questions.

Still open (recommendations standing, no ruling yet): divide/power per-op promotion (`7/2 → 3.5` + `math.intdiv`), curated-boundary section, literal-shape-semantics section. All flagged in the docs as pending.

Files: `plan/dispatch.md` (rewritten), `plan/storage.md` (struct + equality + context + error model), `plan.md` (folder tree, cross-cutting bullets, settled/open, runtime-loading section), `plan/types.md` (serializer-file matrix), `plan/review-opus-4-8.md` (decisions-landed table). Stage status: still not carved — settle the three pending items, then carve.

## 2026-05-28 — Review pass on v1 (23 comments addressed)

Ingi's review of the rewritten plan. 19 of 23 resolved by edits, 4 left open as a single architectural thread.

**Resolved:**
- **Movie example fixed.** Dropped `to console` / `to html` from the .goal — the runtime state (CLI vs web request) decides the channel and serializer, not the goal text. Step 2 in the movie now walks both runs from the same goal text.
- **Image-as-html defaults to path** (link to a static-served file), with base64 inline as the fallback. Smart-detect of "static files allowed for this mime" deferred.
- **path's `this.JsonConverter`** flagged as legacy — it gets absorbed by the new per-(type, format) serializer-file shape once that's settled.
- **Class tree.** Added a folder-by-folder sketch of what the codebase looks like at end-of-branch under "Folder tree after the work."
- **Type registry: discovery-time settled.** Source generator scans `[PlangType]` and emits a static registration table. Matches `[Action]` discovery.
- **HTML deferred** — not real yet; out of scope.
- **`[PlangType]` shrinks to one arg** (just the PLang-facing name) — the CLR type comes for free from the symbol the attribute is on.
- **`duration` confirmed over `timespan`** for LLM-facing name; `timespan` stays as deprecated alias.
- **Cleanups committed:** `datetime` and `duration` get their own folders (parse complexity worth owning); `date` and `time` stay as table-only entries.
- **No PR wording** — flagged by Ingi for the Nth time; updated `feedback_no_prs.md` memory to also cover "don't reference PR as a unit of work in plans."
- **Number's policy uses `app.config`, not a new `environment` tree.** This is the big policy.md rewrite: `Goal` isn't guaranteed thread-safe so a goal-private overlay is wrong; `app.config` already does context → parent → defaults → record-default walking with `IConfig` views (`number.Config : IConfig`). Dropped `[Optional]` (`?` is the optional marker).
- **18-digit precision question** addressed in both policy.md (the escape hatch is `Precision = Decimal` → `decimal` carries 28–29 digits, room for 18) and storage.md (BigInteger slot reserved for the day decimal stops being enough).
- **CLR coverage table** added to storage.md: which CLR numeric types collapse into which storage slot, why the LLM-facing catalog hides the narrow-int / unsigned distinction.

**Open — four-comment thread (one architectural question).** The earlier proposal had a single `IWireWritable.WriteTo(IWriter, ISerializer)` method on the value that switches on mime internally. Ingi pushed back: that's a switch-inside-method smell from far enough away; OBP says distinct (type × format) combinations get distinct files. Alternative: `app/types/<name>/serializer/<format>.cs` per (type, format) — each file owns one rendering, source generator wires the dispatch table, writer carries its format identity, `Wire.Write` does the `(Data.Type, writer.Format) → static method` lookup directly. No interface on the value, no mime switch, no Normalize handshake. I lean this way; proposal posted in the comment threads (plan.md line 84, plan/dispatch.md lines 66 / 70 / 119). Awaiting sign-off before rewriting dispatch.md around it.

Stage status: not yet carved. Next: settle the dispatch shape, then carve stages.

## 2026-05-28 — Reframe: type-as-router, branch renamed to `plang-types`

Ingi reframed the branch's purpose: this isn't about `number` — it's about establishing **higher-level types in PLang** where the runtime is a courier and types own their leaf behavior. `Data { Type = "image", Value = <Image> }` rides through memory untouched; only leaf actions (`math.add` reaches into `number`; `image.resize` reaches into bytes) and leaf serializers (per-format renderers — text/plain → path, text/html → `<img>`, application/plang → base64, protobuf → bytes) dereference. The type owns its serialization (a `data.serialize` style dispatch that forwards to the value); the runtime owns nothing about the kind.

Settled in the conversation:

1. **Type owns its serialization.** New marker `app.data.IWireWritable` parallel to `IBooleanResolvable`. `Data.Normalize` dispatches when the value implements it; the value gets the active `ISerializer` (mime identity) and the `IWriter` (format encoder) and writes its content through the writer's primitive vocabulary. Channel never branches on type; type never knows about channels.
2. **LLM scope shows the bare type, not subtype.** `%photo%(image)`, not `%photo%(image/png)`. Subtype precision lives at the runtime registry layer (Image carries `Mime = "image/png"`) but is hidden from the compile prompt.
3. **No stages this round.** Plan is the discussion artifact; stages get carved after the open questions are settled.

Three proving instances will ship together: `number` (tagged-union, format-uniform), `image` (binary, format-asymmetric — the hardest proof), `code` (text-shaped, semantic-aware). Plus the mechanical cleanups already confirmed: `datetime` → DateTimeOffset, `date` → DateOnly, `time` → TimeOnly, `duration` → TimeSpan.

Branch renamed: `number-type` → `plang-types`. Bot directory moved (`git mv`). Old plan.md fully rewritten; `plan/primitive-vocabulary.md` retired (substance folded into the new spine and `plan/types.md`); `plan/storage.md` and `plan/policy.md` survive as `number`-specific deep dives referenced from `plan/types.md`. New deep dives: `plan/dispatch.md` (the `IWireWritable` contract) and `plan/types.md` (per-type inventory + registration shape).

Four open questions in [plan.md](plan.md) for Ingi to settle before stages:

1. Interface location and signature (`app/data/` vs `app/channels/serializers/`; full `ISerializer` arg vs just mime string).
2. Type registry shape — discovery-time (source generator) vs App-construction-time (assembly scan).
3. Whether to keep number's `NumberPolicy` system or defer policy and ship single-mode arithmetic.
4. Whether HTML grows its own writer on this branch or stays JSON-aliased.

Stage status: not yet carved.

## 2026-05-28 — Primitive vocabulary discussion captured (not decided)

Ingi raised the broader question: PLang's primitive set is not well-defined. He wants TimeSpan, DateTimeOffset (no DateTime), `date` as DateOnly, `time` as TimeOnly, and `image`/`video`/`code`/… as picks the LLM can make. Conversation surfaced that the high-level kind table he remembered isn't deleted — it's `app/formats/this.cs` (30+ Kinds). It just lives next to `app.types.Primitives` instead of being part of it.

Three concepts are colliding under "primitives" today: wire-level CLR types, named LLM picks, and format kinds. Three locations, no single owner — which is why DateTimeOffset is half-registered (`IsPrimitive` accepts it but the name table has no entry).

Open question for Ingi to settle: do we ship `number` first then carve the broader OBP shape later (two arcs), or do we widen this branch to introduce the `app/types/primitive/<name>/this.cs` shape and slot `number` into it as one folder among many (one arc)? Writeup at [plan/primitive-vocabulary.md](plan/primitive-vocabulary.md). No decisions made; nothing in the existing plan changed.

Stage status (unchanged):

| Stage | What | Status |
|-------|------|--------|
| 1 | `app/types/number/` class — storage, parse, operators, IBooleanResolvable | pending |
| 2 | `app/environment/number/` settings home + goal overlay + NumberPolicy resolver | pending |
| 3 | `math.*` retype (canary at `math.add`, then sweep) | pending |
| 4 | Primitives + catalog registration | pending |
| 5 | Compile.llm decimal-literal rule (lands first as precursor) | pending |

## 2026-05-27 — Number type design (plan written, stages pending)

Designed `number` as a sibling-to-`path` category type: real C# class at `app/types/number/this.cs` carrying tagged-union storage (`long _i; decimal _d; double _f; NumberKind _kind`) covering int / long / decimal / double / float. Operators are policy-free (always lenient); a `NumberPolicy` struct with two axes (`Overflow`, `Precision`) and three scopes (app / goal / step) drives configurable behavior. Step-scope is the per-action parameter; app-scope lives on `App.Environment.Number`; goal-scope is a lazy overlay on `Goal` mirroring the existing `Events` pattern.

Key design forks settled in conversation with Ingi:

1. **Rejected decimal-only storage.** Would break IEEE-754 semantics (NaN, Infinity, scientific notation). PLang is general-purpose; can't pick favorites between currency and science. The `number` type spans all numeric kinds.
2. **Rejected builder's "boxed object + Kind" sketch.** Double-allocates (class header + boxed primitive). Picked tagged union with explicit slots — no boxing, strongly typed at every reach.
3. **Rejected architect-imposed promotion rules.** Arithmetic policy is developer-configurable via settings; defaults are lenient, strict mode is one step away. C# operators stay deterministic-lenient; math handlers consult settings and call the policy-aware overload.

Stage status:

| Stage | What | Status |
|-------|------|--------|
| 1 | `app/types/number/` class — storage, parse, operators, IBooleanResolvable | pending |
| 2 | `app/environment/number/` settings home + goal overlay + NumberPolicy resolver | pending |
| 3 | `math.*` retype (canary at `math.add`, then sweep) | pending |
| 4 | Primitives + catalog registration | pending |
| 5 | Compile.llm decimal-literal rule (lands first as precursor) | pending |

Next steps: carve stage files. Order is 5 → 1 → 2 → 3 → 4.

See [plan.md](plan.md) for the spine and design narrative; [plan/storage.md](plan/storage.md) and [plan/policy.md](plan/policy.md) for the deep dives.
