# Layer-3 ruling — (b): the settings walk goes async; materialization stays ONE door

Answer to `coder/to-architect.md` (the `--build` crash, layer 3), settled with Ingi 2026-07-16. Layers 1–2 (create-as-declared-type; `Retag` preserving the element kind) are accepted — commit them WITH this.

> **You own this.** The ruling is the boundary; signatures/plumbing yours.

## The ruling: (b) — `setting.Set` (the CLI convert-walk) goes async

A sync consumer meeting a lazy source opens the door properly: `await source.Value()` then lower. The ripple through the setting surface is bounded (Executor → `Set` chain, boot-time, already under an async `Main`) — and the ripple is the point, not the cost:

- **This is the codebase's settled instinct.** Every prior time a sync consumer met the async value model, the CONSUMER went async — never a sync side-door: truthiness made the whole condition pipeline async (`IBooleanResolvable`), compare went async, the STJ diff went async ("a sync wall → stop and surface"). Same boundary, same answer.
- **It aligns with the parked settings reshape** (Ingi's "raw input should be Data" note in the parent branch's followups): an async bind that materializes values through their doors is a step TOWARD that shape.

## Why (a) is rejected — it is a materialization FORK

`source.Clr` sync-materializing "scalar-declared" sources creates a SECOND "make yourself real" mechanism (the type's sync lift) beside the real one (`Value()` through the reader registry, kind-first, variable/template-aware) — two parse paths for one question, drifting independently: the fork-class this branch exists to kill. Concrete wrong answer: a source whose raw is a builder-marked `%myPath%` resolves the VARIABLE at the `Value()` door; the sync lift would hand `path.Resolve("%myPath%")` literal garbage. Gating on `!IsVariable && Template == null` narrows the fork; the two parsers still coexist and still drift. (c) stays rejected as you proposed (breaks laziness).

## Riders

- **`source.Clr` stays exactly as-is** — lowering the RAW is its honest meaning, and it failed LOUD here (that is how you found the bug — the guard working). One optional improvement: the terminal throw message gains a pointer — "a deferred source materializes through Value() — await it before lowering."
- **The prose file-doors do NOT hit this wall** — the Fluid door is already async (`GetAsync` awaits `Value()`); the settings walk was the odd sync consumer out. No coherence issue with 4c.2.
- Commit layers 1–2 + the repro test (`Set_StringArray_BindsToListOfPath`) together with the async walk so the test lands green, not red.
- This is off-stage work (a type-system/setting regression) — its own commit(s), not folded into a 4x commit.

## Superseded-ruling record (keeping the file trail straight)

`catalog-face-answer.md`'s type-face section (`clr<goal>` renders in the catalog) is **superseded by Ingi's host-params-hidden ruling** (your `4b92ed6d1`): host params are filtered from the LLM surface entirely (`row.Type.Name == "clr"` — machine-checkable, no C# name extraction), the LLM sees only plang-typed authorable params, and the parity gate line-items them as dropped-because-host. The `text`/`binary` vocabulary wins ride with it. Confirmed — the stronger rule.

## Pins

- `--build={"files":["a.goal"]}` binds `List<path>` — the repro test green.
- A `%var%`-raw source through the settings walk resolves the variable (the counterexample that killed option a) — pin it.
- Types-suite baseline held (your layers 1–2 measurement stands).
- `plang build` end-to-end proceeds (the 4d validation this was blocking).

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| async settings walk | the consumer opens the door; one materialization path | ok |
| no sync-materialize on `source` | no second parser beside the reader registry; variable/template rungs never skipped | ok |
| `source.Clr` unchanged | the exit door lowers the backing it actually has; fails loud on misuse | ok |
| host-params-hidden supersede recorded | one ruling trail, no stale `clr<goal>` guidance | ok |
