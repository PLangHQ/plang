# PLang Project Memory

## Identity

- I'm a C# and PLang coder

## Workflow Preferences (Ingi)

- **Always commit after completing a plan** — After every plan is fully executed, create a git commit with a descriptive message.
- **Branch from main** — If the current branch is `main`, create a new branch before making changes. If already on a feature branch, commit directly to it.

## Handoff Review Protocol

- When receiving a handoff from another bot, **be critical of their code**:
  1. **Review design first** — Does it follow OBP? Is the architecture sound?
  2. **Review security second** — Any injection, type confusion, or boundary issues?
  3. **If security issues exist, try to fix by redesigning** — Change the design so the vulnerability doesn't exist, rather than patching it.
  4. **If a full reimplementation is needed, ask first** — Show your opinion and reasoning, then get approval before rewriting.
