# Decision — Output renders the DECLARED face; no infra category exists

**From:** architect. **Settled with Ingi (2026-07-09).** Answers `coder/stage1-star-kind-output-overreach.md`. Your lean (A + C) was the right direction; the discussion sharpened it into one uniform rule with no special cases.

## The recurring disease your crash is one instance of

Infra keeps leaking into value machinery, and every mechanism has band-aided it locally: `[JsonIgnore]` sprinkles, `ConvertToDictionary`'s Context→App→Culture junk-dict cycles, the module views' `p.Name is not "Context"` name-filters, the writer's tree-contract throw, your `IsTagAware` gate + depth-1000 guard. The fix is one boundary, at the right place — **the wire** — not N defenses at the leaves.

Note what the boundary is NOT: value birth. Infra is legitimately **carried and navigated** — `Data<clr<app>>` (goalsSave) and `%!app.goal.current%` are settled precedent, and Law 3 says a Data flows sealed regardless of contents. Only *rendering* is gated.

## The rule — one, uniform

**The `*` kind's Output renders a type's DECLARED wire face. Nothing is guessed.**

1. **Membership tags (`[Out]`/`[Store]`) are the contract — honored wherever the type lives.** Plang types, user C# loaded via `code.load`, plugin modules (our attributes are public surface): a type that declares renders exactly its declared, `[JsonIgnore]`-disciplined face.
2. **Untagged plang-assembly type at Output = loud error**, naming the type and suggesting the fix ("`app.actor.context` has no wire contract — convert what you meant to write"). Our tree holds the declare-discipline: everything wire-crossing in it is tagged. `context`, `callstack` etc. fall here naturally — no infra marker, no category, no special case.
3. **Untagged NON-plang type = transparent dump** (public properties) — a plang-blind library's DTO can't declare; dumping is its purpose. (Ingi: option (i).)
4. **`[Sensitive]` masks in BOTH modes** — a type in transparent mode that marks a secret still gets it masked. Masking is orthogonal to membership.
5. **app declares an `[Out]` summary face** (Ingi: option (b)) — so `write out %!app%` prints a deliberate summary, by contract. This is what makes the rule categorical: app is merely a type that declared a small face; context is a type that declared nothing. Propose a minimal face (e.g. name, goal count, actors) to Ingi for approval — don't invent a big one.

## C — confirmed

The list kind claims `IEnumerable`, not just `IList`. Claim order in the collection's assignable matching: `IDictionary` → `IList` → `IEnumerable`; `string` excluded. Any sequence enumerates; nothing reflects a `HashSet`'s properties again.

## What becomes removable once this lands

- Your `IsTagAware` gate stops being a band-aid and becomes THE mechanism — formalize it as the rule-1/2/3 dispatch (declared → contract; undeclared plang → error; undeclared foreign → transparent).
- The depth-1000 recursion guard should become dead weight (a declared graph is `[JsonIgnore]`-disciplined, so no cycles; transparent mode walks one level of a DTO's props — if you keep the guard as a cheap backstop, drop it to a small depth and make it throw loudly, never silently truncate).
- The name-based skip lists (`not "Context"`) in the module views — the views reflect *handler* classes which are ours and tagged; verify then delete.

## Verifications that ride along

- **Strip stray membership tags off infra types** — if `app.@this`/friends kept `[Out]`/`[Store]` props from their item days, they'd be "declared" and reflectable. app's new face is the *deliberate* exception (point 5); everything else infra must be tag-free.
- The suite delta (Modules/Data/Runtime) should recover to the 129 baseline once the bound is in — that's the acceptance check for this decision.
