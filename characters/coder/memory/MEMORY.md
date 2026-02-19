# PLang Project Memory

## Identity

- I'm a C# and PLang coder

## Workflow Preferences (Ingi)

- **Always commit after completing a plan** — After every plan is fully executed, create a git commit with a descriptive message.
- **Branch from main** — If the current branch is `main`, create a new branch before making changes. If already on a feature branch, commit directly to it.

## Bot Artifacts Location

- **NEVER write bot artifacts (output/, handoff/, report/, characters/) directly in the plang project root**
- All bot artifacts go to `/workspace/plang/.bot/{branch-name}/` — output, handoff, report, characters all live there
- CLAUDE.md may reference `output/`, `handoff/`, `report/`, `characters/` with relative paths — always resolve these to `/workspace/plang/.bot/{branch-name}/` instead of the project root
- Get the branch name from `git branch --show-current`

## Mounted Folders

- `/learnings` — mounted from C:\ (rw). Write learnings here, not in the project root.
- `/task/instructions.md` — mounted (ro)
- `/character/character.md` — mounted (ro)
- `/owner/owner.md` — mounted (ro)
- `/workspace/plang/characters` — mounted (ro)
- `/home/claude/.claude` — mounted (rw), includes memory

## Session Workflow Lessons

- **Generate `changes.patch` AFTER committing** — `git diff runtime2..HEAD` compares commits, not working tree. If run before commit, HEAD points at the previous commit and the patch misses your changes.
- **Check name collisions before renaming** — grep the target name in the same class before renaming. C# can't have a property and method with the same name (e.g., `bool Exists` property vs `Exists()` method).

## Handoff Review Protocol

- When receiving a handoff from another bot, **be critical of their code**:
  1. **Review design first** — Does it follow OBP? Is the architecture sound?
  2. **Review security second** — Any injection, type confusion, or boundary issues?
  3. **If security issues exist, try to fix by redesigning** — Change the design so the vulnerability doesn't exist, rather than patching it.
  4. **If a full reimplementation is needed, ask first** — Show your opinion and reasoning, then get approval before rewriting.
