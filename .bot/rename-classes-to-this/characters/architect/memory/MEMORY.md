# Memory

## Workflow Preferences
- **Don't auto-generate output/report/state files mid-conversation.** Wait for Ingi to signal the session is done or explicitly ask for output. He often wants to keep chatting after a design point lands.

## PLang Design Principles
- **KPR — Kalman Principle of Response**: Every runtime action returns full observable context — result, error — enabling the caller (human or LLM) to self-correct. This applies to MethodRun dispatch, Data returns, error handling — everything returns enough context to course-correct.
- **Everything is lazy** in PLang and OBP. Generated properties, initialization, all lazy.
- **No backward compatibility** on .pr file format changes. Files adapt, old format is not preserved.
- **External libraries mount under `engine.Libraries["name"]`**, never as engine root properties. Only convention-wired built-in code touches engine properties.

## Active Design Work
- [Law of Names](naming_convention_design.md) — naming convention as dispatch mechanism. Class name = `{Owner}{Capability}`, source generator auto-wires properties. Design phase.
