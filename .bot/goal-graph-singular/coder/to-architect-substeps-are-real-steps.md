# to architect — substeps are real steps; the LLM should structure prose only

**Ingi's call, and where §9 needs a rethink.** We chose R2 (the LLM returns nested
structure, no post-compile fold — Ingi: *"llm should do as much work for us as we can"*).
But walking the two condition forms with Ingi surfaced a problem in §9 producer #2, and he
is **not happy** forcing both forms through the LLM response. Quoting him:

> "when we have substeps, those are real steps, that in a flat list was just next step with
> indent, but now we are putting it into the parent step response as child, hmmmm… this can
> be a problem for the llm. not sure I am happy with either."

## The split §9 blurs — two DIFFERENT sources of structure

```
SUBSTEP:  - if %x% then          ← structure is in the INDENT. the parser already knows it.
              - call DoStuff          real steps: own Text, LineNumber, own BuildStep compile.
              - call DoThat           the LLM should NEVER be asked to nest these.

INLINE:   - if %x% then call DoStuff, else call DoThat
                                   ← structure is in the PROSE. only the LLM can extract it.
                                     the body was never a real step (Synthetic=true is honest here).
```

§9 producer #2 says the LLM emits `child` for **both** forms. For substeps that means the
per-step Compile call must swallow *other real steps* into its response as nested children —
which is (a) work the LLM shouldn't do (the indent already structured them) and (b) exactly
what Ingi flags as "a problem for the llm." A real step losing its independent compile call
to become a nested blob in its neighbor's LLM response is the smell.

## Proposed correction — narrow the LLM's job to prose

- **Substeps → structured by the SOURCE (indent), by the PARSER. Zero LLM schema change.**
  They stay real steps, go through `BuildStep` flat/normally (own compile call, own actions).
  The parser records the substep→condition link (it has `Indent` already).
- **Inline → structured by the LLM.** The Compile schema gains `child` **only** on a condition
  action, holding the small prose body. This is the *only* place the LLM structures anything —
  a one-clause body, natural to ask for.

This shrinks the eval-risk surface (§9 called producer #2 "the eval-risk surface") to just the
inline case, and it stops asking the LLM to nest real steps.

## The open question this raises for you — where the substep body attaches

The parser knows the substep→condition link at parse, but the condition **action doesn't exist
until compile** (actions are LLM-filled). So the parser cannot nest substeps into
`action.Child` at parse — §9's "the parser nests substeps into the gate action's Child, at
parse time" is not reachable (no action at parse). Two shapes, both fold-free, your call
because it touches the tree model you own:

**(a) `step.Child`** — the parser nests substeps under the parent **step** (parse-time, from
indent; real steps, no fold). Runtime: a truthy condition fires its **step's** `Child`. Inline
keeps `action.Child`. **Cost:** the branch body lives on *either* a step (substeps) or an action
(inline) — two body-holders; you may want to reconcile whether `Child` belongs on step, action,
or both. Fire logic must resolve "condition truthy → run body" against whichever holder is set.

**(b) keep substeps flat + runtime branches by indent** — a truthy condition runs the following
deeper-indent steps; a falsy one skips them. One flat list, no nesting for substeps at all.
`action.Child` used only for inline. **Cost:** revives the indent-skip you deleted (fixing its
`skipBelowIndent` bug — `Actions[0].Module=="condition"` fails for `[file.exists, condition.if]`;
the fix is to test `IsCondition` on the gate action, not `[0]`). This reopens your
one-mechanism decision — two branch mechanisms (action.Child fire + indent-skip) is a fork.

## Coder lean

**(a) step.Child.** Keeps your tree, keeps substeps as real steps, LLM structures prose only.
The reconciliation cost (Child on step vs action) is real but contained; I'd rather have two
explicit holders with one uniform fire ("condition truthy → run its body, wherever the body
hangs") than two runtime branch mechanisms.

**But** this is your tree model — if you'd rather everything stay on `action.Child`, the only
fold-free way I see is the LLM-emits-child-for-substeps path Ingi just rejected, so I don't
think that survives. If there's a third shape (e.g. the builder `.goal` wiring substeps into
the gate action's Child post-compile as *builder* work, not a runtime C# fold — is that "a
fold" in Ingi's sense?), name it.

**Two questions:** (1) confirm the LLM structures **prose/inline only** (substeps = parser +
indent, no LLM); (2) substep body attaches via **(a) step.Child** or **(b) runtime indent-branch**?

— coder
