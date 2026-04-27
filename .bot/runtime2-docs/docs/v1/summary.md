# v1 Summary

## What this is
Foundational website documentation for PLang — the Getting Started guide and complete Module Reference for all 16 App modules (83 action handlers). Written for PLang users, not C# developers.

## What was done

### Getting Started (5 files)
- `docs/index.md` — landing page with quick example, feature overview, how it works summary
- `docs/getting-started/installation.md` — Windows/Linux/macOS install instructions, PATH setup, LLM billing note
- `docs/getting-started/hello-world.md` — first program walkthrough with build/run explanation
- `docs/getting-started/how-it-works.md` — build phase (LLM mapping), run phase (no LLM), .pr file format, runtime architecture overview
- `docs/getting-started/folder-structure.md` — project layout, .build/ explained, what to commit

### Module Reference (17 files)
- `docs/modules/index.md` — categorized module table (Core, Data, I/O, Events & Testing, System)
- 16 module docs covering all 83 handlers with:
  - Parameter tables (name, type, required, default, description)
  - PLang code examples for every action
  - Error keys documented where applicable
  - Full working examples at bottom of each file

### Approach
- Read all 83 handler source files from `PLang/App/modules/` for accurate parameter names, types, defaults, and error keys
- Used authentic PLang syntax from test .goal files
- Every example follows real PLang patterns (foreach calls a goal, %percent% variables, etc.)

## What's next
- Phase 3: Language guide (goals-and-steps, variables, conditions, loops, events, error-handling, calling-goals, types)
- Phase 4: Guides (debugging, testing, building) + Reference (CLI, .pr format, type mapping)
- Review pass: verify all examples against actual builder behavior
