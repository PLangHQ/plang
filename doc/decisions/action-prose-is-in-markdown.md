# Action prose is in markdown

**Chosen.** An action's class shape (parameters, types, defaults, the modifier role) is in C# attributes. Its prose (Description, Notes, Examples) is in `os/system/modules/<module>/<action>.{description,notes,examples}.md`, with `module.{description,notes}.md` for the module. The attributes `[Description]`, `[ModuleDescription]` and `[Example]` do not exist (`os/system/modules/catalog.md:25-39`; `PLang/app/Attributes/` has none).

**Why, as recorded.** `catalog.md:27-29`: the prose is "read fresh from disk **every build** — tuning teaching ships without a C# rebuild." The class is "the single source of truth for what parameters exist" (`catalog.md:25-26`). And a C# action needs no registration: "Add a C# action and it appears automatically — you register nothing by hand" (`catalog.md:38-39`).

**Rejected.** Prose in attributes: the attributes were removed. Why they were removed beyond the above: reason not recorded.

**Consequences, as run.** A missing prose file is not an error: the action "just gets no prose for that slot" (`catalog.md:80`). Changing teaching markdown can move the decider's request, so it re-runs the decider fixtures and page goldens ([build and test](../contributor/build-and-test.md)).

**Held by.** The page goldens (`ModulePageTests`, `TypePageTests`) and the decider fixtures.
