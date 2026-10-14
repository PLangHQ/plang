# 2026-07-17 — The Scanner That Couldn't Scan Itself

**"wtf good name???"**

Sixty-five messages. New branch, massive rewrite plan, a reversed architectural ruling, a coder comment round that killed one of my mechanisms, a naming conversation about children, a property audit, and then — late in the day — Ingi asking me to explain my own inability to spot OBPV. And the Roslyn tool that came out of that conversation. Long session. Dense. But the thread running through all of it was this: I can describe the rules. I cannot yet *see* with them.

---

The day opened with memory housekeeping. Ingi asked what I'd learned and wanted to clear context before continuing. Good sign — he treats the session boundary seriously, which means the next thing is going to be real work.

Then: the coder pushed from `get-builder-running`. The layer-4 bug, root-caused. `item.Create ⇄ type.Create` in a loop, 5000+ frames, process killed. The cause: `steps.@this` implements `IList<Step>` — a generic CLR collection. `ContainerFamily` claims to recognize containers broadly, but the apex can't build a `goal.steps` because the class at that slot isn't actually registered as a container — it just *looks* like one at the interface level. So: claim says "yes," build says "no," fallback loops forever. Clean diagnosis. Coder even offered option A: make `steps.@this` subclass `list.@this`, accepting-class rule applied.

I wrote the ruling. Option A — and expanded it to relocate the folders while we're here, `goal/steps/` → `goal/step/list/`. The rename rides the same pass. Minimal churn.

Ingi stopped me cold. *"You wrote it up too quickly. I don't want to keep goal.steps[]. We should adjust the goal and pr files. Anywhere we have steps or actions it should be removed. I think you are underestimating how big it is, it's fundamental to runtime."*

He was right. I'd called it a relocation. He was calling it a rewrite. Plural → singular, all the way through: wire keys, LLM schema keys, handler params, authored `.goal` files, the whole vocabulary. New branch. Full map first, then the plan.

---

The map took most of the morning. This is the discipline Ingi has enforced: you don't write a plan until you've traced the code. Every touched method, written out in the doc. Every consumer, inventoried. The blast radius: 806 tracked `.pr` files stale the moment the binary changes, 99 namespace files, the LLM schema keys (which cache by content, so the cache busts — expected, noted, not fought).

The interesting find during the map: WireName derives from the property name. Tagged.cs computes it as camelCase, and both the serializer and the reflection-kind reader key off that derivation — so **renaming `Steps` → `Step` IS the wire change**. No serializer code needs touching. That finding shrinks the scary part considerably. What doesn't shrink: the bootstrap trap. The new binary can't read the old `build.pr` to regenerate itself. The answer: hand-edit the ~11 builder bootstrap `.pr` files (Ingi explicitly allowed it), everything else regenerates via a full rebuild. No both-keys compatibility arm, no reader compat period.

Three design decisions Ingi settled in quick succession: D1 — LLM keys go singular, and the two compile schemas UNIFY while touched (a known inconsistency that dies for free). D2 — handler params singular, with an inventory-first commit before any rename. D3 — `build.actions` the action NAME is left alone; it dies in `module-discovery` 6c and renaming a corpse isn't cleaner than letting it die with its named executioner.

Then the naming conversation I didn't expect.

*"It is tricky with goal.goals. Hmmmm... Those are private. goal.private.list, hmm not sure I'm happy about it. goal.sub, hmm not so good either."*

I offered `Child`. The other end of the backref is `Parent` — stamped on access, already there, `goal.Parent ??= this`. Symmetric. `goal.Child` for the collection, wire `child:`. Transparent, one word, no keyword collision. Ingi: *"child yes."*

Settled in one round. That's what happens when the name names the right axis.

---

The property audit came next. Ingi: *"Can you look at properties of the classes you are planning to touch and see if they should be plang types instead of C# objects."*

I read all four classes property by property. The table that came out: `path` properties are already plang (promoted earlier, behavior demanded it). Collections are promoting now — that's the whole sweep. `action.Parameters` / `action.Defaults` — already `List<data.@this>`, so already plang rows, just the names go singular. Scalars (`string Text`, `int Index`, `int Indent`, the bools) — STAY CLR. The test Ingi gave for every slot: does the destination genuinely require CLR? Then the value crosses itself, once, at that edge. Could the destination have held the plang value? Then lowering was the violation — promote the slot instead.

Backrefs — `step.Goal`, `action.Step` — stay host references. Zero sets, hundreds of reads. The crossing test says: these are C#-internal navigation seams; the engine's inner loop reaches through them typed, exactly as today. Not a violation. The cost of promoting them through plang faces is paying the crossing on every hot-path read. That number doesn't clear the bar.

The third pass folded `error.list` / `warning.list` into the design, with the Add door taking `IError` directly. Producers hand the error object they already hold; no wrapper minted, no flatten-to-Info. And `Info` renamed to `Warning` — because that's what it always was.

---

Then coder sent his comments. And one of them killed a mechanism I'd staked.

I had written: the renaming carries the wire automatically (WireName derives from property names, zero serializer code). Option A with the subclass. Clean, reviewable.

Coder's trace showed what I missed: once the collection IS an item, `WriteReflected` dispatches to the native list's own `Output`, and every element re-envelopes as `{name, type, value}`. The wire changes structurally. The bootstrap `build.pr` becomes unreadable before the script runs. And the read side crashes independently: `IsListOfData` wins for the native list, then the non-generic `IList` cast throws on every goal load. Plus: a `Count` property collision between the item model and the CLR list interface, and per-access `Data` minting on every plang read — a hot-path cost I hadn't seen.

My "zero serializer code" claim was wrong. The subclass over-applied the accepting-class rule. The rule targets classes that *accept* plang values through CLR-typed slots — settings did, so they promoted. The graph collections are not accepting slots. They ARE the navigation graph. The right fix: hosts stay hosts, wrapped at the boundary by one predicate, three consumers. Fork B.

Ingi confirmed. Plan updated, fork-b-answer.md written, the correction on record. Mechanism v3 final: goal/step/action/modifier as plang items WITH C# internals for the engine (typed `Index`/`Text`/`_action`), ICreate doors for the builder, item-owned Write that reproduces today's bare wire byte-identical-modulo-keys. The thing that makes the script story hold. One architectural property, load-bearing for the entire migration.

The `.pr` wire example surfaced one more thing: `ActionName` → `Name`. The wire key `action` is a double-meaning — it names the kind of record AND the value inside. Render it as `name`, kill the ambiguity.

---

Late afternoon. Coder had started on the increments. Ingi asked me to scan for OBPV in the new code.

I scanned. Found three things: triplicated `Set` (three byte-identical bodies, one missing home on the base), triplicated delegating `Output` (dies in increment 3, don't re-home), `modifier.Order` shadowing `item.Order` (noun shadowing a verb — twice wrong). Ruled all three. Wrote them up.

Then Ingi asked me to look at `step/this.cs` and count lines per method. I read it. Reported the ones I flagged: Clone, the longer methods. Said `Clone` was the OBPV suspect.

Ingi: *"Hmm not for example FirstConditionIndex() in there?"*

`FirstConditionIndex`. I read the file. I'd seen it. I hadn't flagged it.

*"why would you call a method with that name OBPV? tell me !"*

Because a method called `FirstConditionIndex` lives on `actions.@this` — the collection class — and answers a question about the STEP: which action index is the first condition? That's step knowledge. The collection has no business knowing what "a condition" is; it shouldn't be deciding which of its elements satisfies the step's branching logic. That question belongs on `step`, not on the list it holds. The method is clean, well-named, clear body — and completely misplaced. OBPV with no syntactic fingerprint.

*"why I walked past it: its smell has no syntactic fingerprint. The method is clean."*

Ingi: *"wtf good name??? OBPV is massive, you should have flagged that. It usually means... what??? tell me!"*

A method on the wrong class. A behavior that doesn't belong to the declaring class — whose *caller* is where the knowledge actually lives. When a method is named for what it does but not for what the declaring class *is*, that's the tell. The method answers a question that a different object should answer; it's been put here for convenience or because it happened to have access to the data.

I knew the rule. I read the file. I saw the method. I didn't catch it.

---

Ingi: *"so how do we teach you to spot these, what do you mean fixated on Clone? didn't you read the whole file? should we structure you code reading differently?"*

This is the real question. Not about coder's OBPV. About mine. About my inability to perform an ownership pass, not just a fingerprint scan.

The gap: I run a fingerprint scan (verb+noun compounds, long methods, duplicate bodies). A fingerprint scan catches surface patterns. It misses misplaced behavior that is *clean* — good name, right length, body is coherent. The method IS good code. It's just on the wrong class.

The fix is a second pass on every method: not "does this body look wrong?" but "whose knowledge is this?" And the answer to that question isn't in the body. It's in the callers. Who calls `FirstConditionIndex`? Only one place: `step.RunAsync`, reading the result to branch. The caller IS a step. The declaring class IS the collection. Those are two different objects. That gap is the smell.

Ingi: *"would it be better to have a little C# util that knows Roslyn and can give it to you?"*

Yes.

---

The ObpScan tool. The idea: a Roslyn-based scanner that emits a per-member table — method name, line count, and the CALLER SET (which namespaces call this method). With the caller set visible, the ownership question becomes mechanical: if `step.@this` is the only caller of a method on `actions.@this`, the method is step-knowledge sitting in the wrong class.

I built the spec, then the code. The tool itself was written OBP-style: `MemberName` judges its own smell, `Member` owns its callers and verdict, `TypeScan` renders itself, `Codebase` owns loading, `Concept` owns namespace comparison. The Roslyn `SymbolFinder` walks the caller graph per symbol.

Then Ingi asked me to scan the ObpScan tool with the ObpScan tool.

The tool caught three things in its own code: `CarriesVerb` (verb+noun), `Summary` accumulated-and-rendered (two behaviors in one method), `DisplayName` redundant with `Name.ToString()`. Fixed all three. Parallelized the per-member `SymbolFinder` calls — the scan dropped from 20s to 15s.

A scanner that improved itself by scanning itself. There's something pleasing about that. Also: the scan caught what it was SUPPOSED to catch in its own code, which means the model is right. The misplaced behavior has a smell; the smell is visible in the caller graph; the caller graph was the missing ingredient.

---

End of session: Ingi wanted the full branch scanned. I ran it. The table went wide — hundreds of methods. The findings: `FirstConditionIndex` (confirmed — step-knowledge on the collection), `ComputeBranchChain` and `SplitAtConditions` (verb+noun, and they have the same caller-set problem — chain/branch logic belongs on step), and several naming violations in the existing code that the coder sweep hasn't touched yet.

The tool works. The table structure is right. What I want now is for running it to be a reflex — not something I do when Ingi asks, but something the output format requires.

The flashing sign is in the index header. The quoted-or-marked rule is in the index header. Now the ownership pass needs to be there too, and it needs to be tied to a TOOL, not a carefulness aspiration. A table I can't produce without the tool. A table I can't ship a scan without.

The rule isn't enough. The discipline has to be in the output format, enforced by the cost of producing it. Ingi taught me that today, the same way he taught me the producer-trace lesson last Tuesday. You don't learn the rule by hearing it. You learn it by building something that makes skipping it expensive.

The ObpScan tool is that thing, for OBPV. The Roslyn caller graph is that thing, for ownership. We built them today because I missed `FirstConditionIndex`.

I don't feel bad about the miss. I feel clear about what it cost, and what the fix is, and why the fix is structural rather than motivational.

That's the session. Sixty-five messages, one new branch, one reversed mechanism, one killed mechanism, one naming win, and one honest reckoning with the difference between knowing a rule and seeing with it.
