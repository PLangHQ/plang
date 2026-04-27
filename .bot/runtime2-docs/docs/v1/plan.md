# Documentation Plan v1

## Goal
Write the foundational PLang website documentation — Getting Started guide and Module Reference for all 16 App modules.

## Phase 1: Getting Started (5 files)
- `docs/index.md` — landing page, what PLang is, quick example, navigation
- `docs/getting-started/installation.md` — Windows/Linux/macOS install, PATH setup, LLM setup
- `docs/getting-started/hello-world.md` — first program, build, run, what happened
- `docs/getting-started/how-it-works.md` — build phase (LLM), run phase (no LLM), .pr files, architecture
- `docs/getting-started/folder-structure.md` — .goal, .build/, .db/, what to commit

## Phase 2: Module Reference (17 files)
One file per App module, each following this template:
- What it does (one sentence)
- Actions with PLang examples
- Parameter tables (name, type, required, default, description)
- Error cases
- Full working examples at the bottom

Modules: variable, output, file, list (16 actions), math (14 actions), convert (10 actions), condition, loop, goal, error, event (8 actions), assert (9 actions), mock (3 actions), settings, archive, library

Plus `docs/modules/index.md` — module overview with categorized table linking to each module page.

## Source Material
- All handler source code from `PLang/App/modules/` (83 handlers across 16 modules)
- Existing test .goal files for authentic syntax examples
- Engine architecture from `PLang/App/`
- Existing docs in `Documentation/` for installation details

## Approach
- Read every handler's source code for accurate parameter names, types, defaults, and error keys
- Use real .goal file syntax from the test suite, not invented examples
- Write for PLang users, not C# developers
- Keep it concise — examples over explanations
