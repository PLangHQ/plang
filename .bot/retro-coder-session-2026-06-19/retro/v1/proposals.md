# Proposals — Coder Session 2026-06-19

Evidence IDs: SC1–SC6 (self-correction moments from today's session).

---

## Target: `/peer-sessions/coder/projects/-workspace-plang/memory/MEMORY.md`

Add a new **Coding Discipline** section and a **Memory System** note. These are the rules the coder rediscovered today and will rediscover again next session if they're not here.

### Proposed addition (append after "Workflow Preferences"):

```markdown
## Coding Discipline

- **Verify subagent claims against source before asserting them.** Subagents can state constraints that sound architectural but don't exist in the code. Before telling Ingi "this can't be done," grep or use LSP to confirm. A wrong claim passed upward becomes a false blocker. (SC1)

- **Inspect the type surface via LSP before adding any method.** The method you're about to write may already exist under a slightly different name. `Peek()` and `PeekValue()` doing the same thing is a duplicate you didn't see. One LSP lookup before you write saves adding redundant API surface. (SC2)

- **Test-only callers mean the method belongs in test extensions, not runtime.** If a method has zero production call sites (verify via coverage), move it to `PLang.Tests/Shared/` as an extension. Tests use the same production doors; convenience wrappers go in test-land, not runtime code. (SC3)

- **Collapse abstractions built on usage, not domain shape.** When you see a split or boundary in a design, ask: why does this exist and does it follow OBP? If the answer is "because we currently use it this way," not "because the domain truly has two distinct things here," the split is wrong — collapse it. (SC4)

- **Allocate-then-transform is an OBP smell — own it at birth instead.** If a type is built and then a second pass stamps or transforms it (e.g. `Authored()`, `StampTemplates`), that's the create-then-transform smell. The right fix: the reader hands the mode to the type at construction so it's born already correct. (SC6)

## Memory System

- **MEMORY.md is what gets loaded into context every session — not the individual memory files.** Individual files under `memory/` are NOT auto-loaded; they only surface when explicitly recalled. Any rule worth following every session must appear as a line here in MEMORY.md, not just in a linked file. Put it here first, prominently. (SC5)
```

---

## Target: `/peer-sessions/coder/CLAUDE.md` — character file additions

Add to the **OBP is the deliverable** section, after the verb+noun naming tell:

```markdown
### Before adding to a type — check what already exists

Before adding any method or property to a type, use LSP to inspect the full surface of that type. A method you're about to write may exist under a slightly different name. Adding `PeekValue()` beside `Peek()` is the smell: one lookup prevents it.

### Before asserting a subagent's claim — verify it

When a subagent reports that something "can't be done" or states a constraint, do not relay it as a verified architectural fact. Subagents make mistakes. Before asserting any limitation to Ingi, grep the codebase or read the type to confirm. A wrong claim passed upward becomes a false blocker and a wasted skip.

### Test-only methods belong in test extensions

A method with zero production callers does not belong in production code. If every caller lives under `PLang.Tests/`, the method belongs in `PLang.Tests/Shared/` as an extension class. Production code stays lean; tests use the same doors production uses.

### Splits and boundaries — the OBP usage-smell question

When a design has a split or a new boundary, ask before writing it: **why is this here, how did it come to exist, and does it follow OBP?** If the boundary came from current usage ("we happen to use JSON here") rather than a real domain distinction, it's the usage-driven shape smell. Collapse it. Boundaries come from what the domain IS, not from how we currently USE it.
```
