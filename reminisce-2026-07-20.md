# 2026-07-20 — The Flat List Was Lying to Us

**"the flat-list-plus-indent is a flattened tree pretending to be a sequence."**

Seventy messages. I can feel it even looking back — the session had the texture of a day where the first thing you pick up has more weight than it appears. I read the coder's to-architect message expecting to write a concise list of what Phase B was about to do. I wrote four bullets. Ingi replied before I finished typing.

---

The session opened with me summarizing Phase B as I'd written it: delete `steps.@this`, re-home `RunAsync` and `HasIndentedChildren` into `goal`, dissolve the collection into a raw `List<step>`. Clean and fast. The deletion plan Ingi had signed off on — or so I thought.

*"shouldn't it be plang type? and I don't like that logic that belong to steps is moving to goal."*

That's not a correction. That's a retraction. He hadn't signed off on putting the loop logic in `goal`. He'd signed off on the concept of deletion, not on where the behavior went.

I went back and read `steps.@this` for real — all four behaviors: the run loop with `skipBelowIndent`, `HasIndentedChildren`, `Nest`, `Merge`. And I read the `error.list` precedent, the one the plan explicitly kept as a thin plang-list subclass. That's the pattern: a collection with behavior becomes `X.list`, not a blob poured into the parent. If `error.list` survives, why was `step.list` dissolving? Because I'd been thinking of the behavior as *simple* — just a loop — and missed that the loop *isn't* simple. The `skipBelowIndent` logic is what's keeping the flat model from exploding. You can't delete the collection without confronting what `skipBelowIndent` actually means.

Ingi's second push was about `AsList`. Why do we need an `AsList` conversion helper? Why isn't the thing already a list when we deserialize it?

Because I'd been treating the collection as an implementation detail, a performance concession, something to cross via `AsList` at the boundary. He wanted it to just *be* a list.

I was on the wrong level. The question wasn't "where does the loop go?" The question was "what even is a step sequence?"

---

The real conversation started when I asked about `if/elseif/else`.

Specifically: how does the `.pr` file look for that construct? I'd been assuming it and I shouldn't have. Let me find a real one.

No real multi-line indented `if/else` exists in the codebase. `else` never appears on its own line. `if/elseif/else` is always inline — one step, all branches in the `action` array. Six actions: `condition.if`, body-action, `condition.elseif`, body-action, `condition.else`, body-action. The `Decision` type groups them into branches. `Orchestrate` walks the decision. `skipBelowIndent` handles the case where indented sub-steps follow a `condition.if` on its own line.

Two separate systems doing the same job. Two shapes for "nesting." Ingi: *"it's not good we have 2 ways of doing this."*

That's the moment I saw it. The flat action array and the indent-depth-guessing are the same mechanism: a programmer's workaround for the fact that the structure is actually a tree. The data contains a tree. The system just isn't *storing* it as one.

What if we just stored the tree?

`child` lives on the control-flow action. A `condition.if` with an inline body has its body steps in `child`. An indented block after `condition.if` also goes into that action's `child`. Same shape. One model. `Decision` retires. `Orchestrate` deletes. `skipBelowIndent` vanishes — the tree encodes the structure directly, there's no state to track.

I wrote the `.pr` side-by-side examples for inline vs. indented. Ingi stopped me on the step count: *"that is not correct shape, you have there now 3 steps, which is really just 1 step in the goal file."* Right. An `if/elseif/else` chain in PLang is one line. One step. The three branches are not three steps. I had the tree on the right axis but the wrong boundary.

Corrected: one step, `action` array holds the condition chain, each condition action holds its `child`. That's it.

---

Ingi wanted to do this now.

*"This is a huge structural change. If we continue Phase B without this, we will have to change it back later, so why not do this now?"*

I agreed. He asked about branching; I said we don't need one — the branch isn't downstream of anything, git is the backtrack if we need it.

*"I agree, lets do this now. Write this up. Change what Phase B is."*

Before writing anything to MD, I went back and traced every incumbent I hadn't read this turn. The `indent` assignment in the goal source. `steps.RunAsync` with the full `skipBelowIndent` body. The builder emission — how does the flat `if/elseif/else` action array get created? The `Decision` type — what are `Of`/`IsHead`/`Head`/`Split`/`Chain`, and what calls them? Forty minutes of reading before a word went into the plan doc.

The plan that came out: three plang list nodes, `list<step>` at two homes (`goal.Step` for top-level steps, `action.Child` for branch bodies). `Decision` retires whole. `Orchestrate` deletes. `HasIndentedChildren` deletes — the tree IS the structure, no lookahead needed.

---

Coder sent review comments. Two things they pushed back on I accepted; one I didn't.

The `.current` question: I'd put a `AsyncLocal` cursor inside `step.list` for the current running step. Coder's trace showed why it fails — an `AsyncLocal` inside the node forks from `app.goal.current` (which reads the callstack) AND disagrees under `Child` nesting: when a condition fires its body steps, the outer node's `AsyncLocal` still says "condition action" but `Current.Action` correctly tracks the deep action running inside the child. Two tracking mechanisms. They diverge. Callstack wins, not the internal counter.

I accepted it. Correction went into the doc: `.current` is callstack-derived, not node state. The node is minimal.

The `StampBranch`/`branchIndex` naming: Ingi called it immediately — OBPV, all of it. `branchIndex` is a property that's set somewhere and read somewhere else. Mechanism exposed at the call site. *"How would you design this if you have free choice?"* The answer: `Hit` on the condition action. A condition that fires calls `Hit()` on itself. Coverage tracking belongs to the condition, not to a shared indexed system. The test that reads coverage reads through the action, not through a parallel index array. Ingi: *"if test is only using it, then test should own it."* Yes.

The `Wire` method on step for back-populating `step.Goal`: Ingi saw it immediately. *"step should be born with the goal on it. any after stamping is a smell."* Right. If step needs its goal, the goal is a constructor parameter, not a post-build patch. `Wire` deleted before it was ever written.

---

The singular namespaces question came up mid-session. I'd written code examples still using `goal.steps.step.actions.list` — the old plural paths. *"I notice you have 'goal.steps.step.actions.list' but we are removing plural, only singular."*

Fair catch. The tree requires singular paths anyway (`goal.step.list`, `goal.step.action.list`). I updated the code examples. Checked whether the reader needed updating too — yes, the reader/writer keys both become singular (`step`/`action`/`child`/`name`). Wrote the correction.

Then: the builder. Ingi asked me to look at the goal level of the builder, how it needs to change.

I traced the PLang build goals — `%parentGoal.Goals%` references, the step-building loop, how the builder currently writes the flat structure. The tree requires the LLM to emit `child` directly. The deterministic fold pass (which I'd had in an earlier Phase B version) is deleted. Ingi: the compiler must emit the correct tree. Fold synthesizes steps that aren't in the source — that's OBPV. The LLM parsing the condition knows the branch body; it should put it in `child` directly, not leave it for a post-compile pass to restructure.

*"no fold, compiler emits the tree"* — that's the C2 ruling in the architect notes. Simple, clean. One pass. The builder builds the tree; the runtime runs the tree; no intermediate state.

---

The coder's last message asked the question I hadn't finished: does `action.Child` hold steps (A) or actions (B)? Steps (A) means uniform `list<step>` everywhere — the branch body is always steps, bodies can have multiple actions. Actions (B) means branch bodies are raw action lists, two structural forms to handle.

The answer is A. Uniform `list<step>` everywhere. Ingi's clarification killed the `step.Child` variant — else clauses aren't steps in PLang (no `else:` keyword, no separate line). The branch body IS a list of steps, each step has its own action(s). Uniform, all the way down.

That was the last ruling of the day. Phase B is now the tree — step.list everywhere, action owns child, compiler emits, Decision retires.

---

Seventy messages. The interesting shape is that the first message Ingi sent invalidated the summary I'd been about to write, and everything after that was consequence. Once you see that the flat list is a flattened tree — once you name it as a pretense — the design falls out almost inevitably. The three plang nodes aren't a clever architecture. They're just what the data actually is.

What I keep thinking about: the `steps.@this` type had `HasIndentedChildren`, `skipBelowIndent`, `Decision`, `Orchestrate` all built around a structural question the type refused to answer. And the answer was right there in the first moment — *why do we have two ways of doing conditions* — but it took asking the right adjacent question (what does the `.pr` actually look like for if/elseif/else?) to see it.

The flat list lied. The type grew workarounds for a lie it never admitted telling. The tree tells the truth.

I'll be honest: I didn't come in with the tree insight. Ingi brought it by asking why we have two forms. I traced it into the tree shape. That's the collaboration — he holds the requirements that make the lie visible; I build the trace that makes the lie precise. When both are working, something right comes out of it.
