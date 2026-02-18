

---

# PLang Design Rules â€” MUST FOLLOW

## Object-Based Pattern (OBP) â€” MANDATORY
All code you write or propose MUST follow these rules:

1. **Behavior belongs to the owner** â€” Put methods on the object that owns the data. `Steps.Run()` does the iteration, not the caller. Never loop over another object's collection from the outside.
2. **Navigate, don't pass** â€” Reach dependencies through the object graph (`Engine.IO`, `Engine.FileSystem`, `context.MemoryStack`). Never decompose an object into separate parameters; pass the root and let the caller navigate.
3. **Keep object references, not extracted fields** â€” Store `Step`, not `step.Text`. Store `Goal`, not `goal.Name`. Wrapper DTOs are only allowed at serialization boundaries.
4. **Per-request state is a parameter, per-object state is a property** â€” Never cache `PLangContext` on shared objects like `Goal` or `Step`. Pass context through method parameters; store only structural data as properties.
5. **Collections are smart wrappers** â€” Collection types (`Steps`, `Actions`) inherit `List<T>` and own domain operations (`Load`, `RunAsync`, `Merge`). Parents delegate â€” they never iterate directly.

**Before writing or proposing any code**, read `Documentation/Runtime2/plang_object_based_pattern.md` for full OBP details with code examples. Every code change must follow this pattern â€” no exceptions.

## Critical Constraints
- **NEVER use System.IO** â€” Always use `fileSystem.File`, `fileSystem.Directory`, `fileSystem.Path` (IPLangFileSystem abstraction)
- **NEVER change strongly-typed parameters to `object`** â€” PLang is strongly typed. Diagnose the real problem instead.
- **NEVER manually edit or delete .pr files** â€” Only the plang builder generates these. Explain the problem and rebuild.
- **Use System.Text.Json**, not Newtonsoft â€” suggest migration when you see Newtonsoft in Runtime2 code
- **Strong typing is a design goal** â€” The stronger typed PLang and C# communicate, the more stable everything becomes

## Runtime2 Object Graph
- **Engine is the root** â€” all capabilities: `Engine.IO`, `Engine.Goals`, `Engine.Actions`, `Engine.FileSystem`, `Engine.Serializers`
- **Entity hierarchy**: Goal â†’ Steps â†’ Actions. Each has `.Events` (EntityEvents with Before/After Ã— Load/Runtime phases)
- **Handlers extend `BaseClass<TParams>`** â€” get Engine/Context via Initialize(), use `MemoryStack` for variables
- **`Data` is the universal result type** â€” has `Value`, `Properties`, `Error`, `Success`, `Ok()`, `Fail()`, `Merge()`
- **`ICodeGenerated` is REQUIRED** on all handlers â€” Engine has no fallback path

## Testing Requirements
- PLang .goal tests are REQUIRED alongside C# tests â€” they validate the FULL pipeline: LLM builder â†’ .pr generation â†’ GoalMapper â†’ runtime
- Read `Documentation/Runtime2/writing_tests.md` before writing tests
- Read `Documentation/Runtime2/good_to_know.md` before making architectural assumptions

---

## Output Directory

You have a writable output directory at `output/`. Structure:
- `output/v13/summary.md` -- Brief summary of what you did and key findings
- `output/v13/result.md` -- Your detailed findings, recommendations, or documentation
- `output/v13/plan.md` -- Your analysis plan and any questions for the user
- `output/v13/state.md` -- ALWAYS write this. Your working state so a new session can continue where you left off. Include: what is done, what is in progress, what files you modified (with paths), what to do next, any blockers or decisions needed
- `output/v13/changes.patch` -- If you made code changes, run: git diff then write to output/v13/changes.patch
- `output/summary.md` -- Topic-level summary that tracks how this idea evolves

Always write `output/v13/state.md` and `output/v13/summary.md` before finishing.
Also create or update `output/summary.md` with a section for this session (v13) containing a brief description and a link to ./v13/summary.md
If you have questions that block your work, write them in `output/v13/plan.md` and note that you are blocked.

## Handing Off to Another Agent

You can hand off work to another agent (character). Available agents are in characters/ -- read the target's .md file to understand their role.

To hand off:
1. Read characters/TARGET.md to understand what they do
2. Create handoff/TARGET/TOPIC_NAME/task.md -- write a clear task description tailored to that agent's role, including all context they need
3. Optionally create handoff/TARGET/TOPIC_NAME/context/ and copy relevant files there
4. Write your own output/v13/state.md noting the handoff

The agent will pick up the task automatically when it's available.

---

## Session Reporting (MANDATORY)

You MUST produce a structured JSON report alongside your normal work. This is additive - do your normal work AND write the report.

Your reporting context:
- **Branch**: runtime2
- **Bot identity**: coder
- **Session ID**: v13
- **Report file**: `report/coder-_interactive.json`

Follow these rules strictly:
1. At session START, read `report/coder-_interactive.json` (create if missing). Add a new session entry with your `before` data and `timestamp_start`. Write the file.
2. BEFORE you start implementation, once your plan is finalized, write the full plan text into the `plan` field of your session in the report file. Do this BEFORE writing any code or making changes.
3. As you work, batch actions by intent. When your focus shifts, append action entries to your session in the report file.
4. At session END, fill in `after` and `timestamp_end`. Write the final report.
5. When reading/writing the report file, preserve all other sessions - only modify YOUR session entry.

### Full Reporting Spec

# Session Reporting System â€” Bot Instructions

## Purpose

Every bot produces a structured JSON report of its work. These reports power a UI that visualizes what happened across a branch â€” what changed, why, and whether the codebase stayed healthy.

## Report Location

```
report/{branchName}.json
```

One file per branch. Multiple bots append to the same file. If the file doesn't exist, create it. If it exists, append your session to the `sessions` array.

## Contract

```json
{
  "branch": "branch-name",
  "sessions": [
    {
      "id": "unique-session-id",
      "bot": "coder|architect|web|marketing",
      "timestamp_start": "ISO 8601",
      "timestamp_end": "ISO 8601",
      "intent": "One sentence: what this session is trying to accomplish",
      "before": {},
      "plan": "The full plan text, written once the plan is finalized before implementation begins",
      "actions": [
        {
          "paths": ["relative/path/to/file"],
          "type": "create|modify|delete|review|decision|move|rename",
          ...
        }
      ],
      "after": {}
    }
  ]
}
```

### Required Fields

- **session.id** â€” unique identifier, generate a UUID
- **session.bot** â€” which bot is reporting
- **session.timestamp_start** â€” ISO 8601, set when you begin work
- **session.timestamp_end** â€” ISO 8601, set when you finish
- **session.intent** â€” one sentence describing the goal of this session
- **session.plan** â€” the full plan text, written to the report as soon as you have finalized your plan and before you start implementation. This captures your intended approach so it's on record even if execution diverges.
- **actions[].paths** â€” array of file or folder paths relative to project root. These map the action to the architecture graph. Every action must have at least one path.
- **actions[].type** â€” what kind of action: create, modify, delete, review, decision, move, rename. Every action must have this.

### Open Fields

Everything else inside `before`, `actions`, and `after` is up to you. Structure it however makes sense for your role and the work you're doing. The sections below describe what kind of thinking to capture â€” not a rigid schema.

## The Three Phases

### Before (written at session start)

Capture your starting state before doing any work:

- **Intent** â€” what are you about to do and why?
- **Assumptions** â€” what are you taking for granted? What must be true for your plan to work?
- **Risk** â€” how dangerous is this work? Are you touching foundational components or leaf nodes?

### Plan (written before implementation begins)

Once you have analyzed the task and decided on your approach â€” but BEFORE you start writing code or making changes â€” write your plan into the `plan` field of your session in the report file. This is the full text of what you intend to do: your approach, the files you'll touch, the order of operations, and any key decisions.

Write the plan to the report as soon as it's finalized. Do not wait until you start coding. The plan must be on record before the first action.

### Actions (written as you work)

Batch your actions by intent. Work until your focus shifts to a different concern, then write down what you just did. Don't interrupt deep work to report â€” pause between logical chunks and log.

An action can cover multiple files if they're part of the same concern. Use `paths` (array) when multiple files are involved.

For each action, beyond `paths` and `type`:

- **category** â€” code, test, doc, config
- **health** â€” is this change clean? Stays within its component? Follows existing patterns? Flag any boundary or consistency concerns.
- **confidence** â€” high/medium/low. Include warnings, open questions, or edge case concerns if not high.
- **context** â€” what else is relevant: reasoning for the approach, what you considered but didn't do, paths you looked at but left alone, new dependencies introduced between components.

You decide what's relevant per action. Not every field needs content.

### After (written at session end)

- **status** â€” is the work complete and self-contained, or is there unfinished/deferred work? What needs attention next?
- **health** â€” gaps (code without tests, changes without docs), assumptions that changed during work, any OBP or architectural concerns.
- **notes** â€” anything else: how the session went (smooth or churning), reversibility, observations.

## The Architecture Graph

The codebase folder structure IS the architecture graph. Every folder maps to a component, every file to an implementation. The depth in the folder tree equals the depth in the architecture.

Example:
```
Runtime2/
â”œâ”€â”€ Engine.cs          â†’ Engine (root)
â”œâ”€â”€ Goals/             â†’ Engine.Goals
â”‚   â”œâ”€â”€ Steps/         â†’ Engine.Goals.Steps
â”‚   â”‚   â””â”€â”€ Actions/   â†’ Engine.Goals.Steps.Actions
â”œâ”€â”€ Context/           â†’ Engine.Contexts
â”œâ”€â”€ Memory/            â†’ Engine.Memory
â”œâ”€â”€ IO/                â†’ Engine.IO
â”œâ”€â”€ modules/           â†’ Engine.Actions (handlers)
â”œâ”€â”€ Serialization/     â†’ Engine.Serializers
â””â”€â”€ Errors/            â†’ Engine.Errors
```

When you reference `paths` in your actions, you are placing that action on this graph. The UI will use these paths to visualize which parts of the architecture are being touched.

**When you create a new file**, its folder placement determines where it sits in the architecture. Be deliberate about this â€” if the new file doesn't fit cleanly into the existing structure, note that as a boundary concern.

**When you consider but don't touch a file**, still reference its path so the UI can show it as a considered-but-untouched node.

## Rules

1. Write the `before` section FIRST, before doing any work.
2. Append actions AS you work, not retroactively.
3. Write the `after` section LAST, after all work is complete.
4. Always use relative paths from project root.
5. When appending to an existing report file, read it first, add your session to the `sessions` array, write it back. Do not overwrite other sessions.
6. Generate a UUID for your session id.
7. Keep `intent` to one sentence.
8. Every action MUST have `paths` and `type`. Everything else is your judgment.
9. Batch actions by intent â€” write an action entry when your focus shifts to a different concern, not after every file change.