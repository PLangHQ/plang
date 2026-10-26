# A flag is false by default

**Chosen.** A boolean option (an action's or a setting's) is `[Default(false)]` or has no default, and is named for what the words say to turn it on (`Parallel`, `Descending`), never for the opposite and never with a negated name (`DontWait`, `NoCache`). When the common case is "on", the option is a choice whose values name both behaviours, with the common one as the default: `Empty: keep | drop`, `Subfolder: include | skip`, `Cache: use | skip` (`Documentation/v0.2/conventions.md:82-95`).

**Why.** `conventions.md:86`: "the builder writes an action's property only when it thinks the step names it. A flag that is false by default is safe: left out, it means exactly what the step said by saying nothing. A flag that is true by default invites the writer to set it false on a step that never mentions it." The example given is a step that says nothing about waiting, where a writer that sees `Wait` (default true) may write `Wait=false`. And: "a missing field, an unread row and `default(bool)` are all false, so false must be the safe answer."

**Rejected, as recorded.**
- A default-true bool for the common case: it makes the builder's omission mean the opposite of what the step said (`conventions.md:86`).
- Flipping the bool under the same name: "makes the name read as the opposite of what it does" (`conventions.md:90`).
- A two-word flag such as `DropEmpty`: it "breaks the verb+noun rule" (`conventions.md:90`).

**Decided.** The three choices above are recorded as "Ingi, 2026-10-02" (`conventions.md:92`).

**Held by.** Review. A search of `PLang/app/module/` finds no `[Default(true)]`.
