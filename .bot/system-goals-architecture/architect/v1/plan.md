# Plan: Revise Everything is Data Proposal

## Goal
Rewrite `Documentation/Runtime2/proposals/everything_is_data.md` to reflect the refined design from the architecture conversation. The original proposal had several ideas that were discussed and either refined or rejected.

## Key Design Decisions (from conversation)

1. **Data<T> without CRTP constraint** — Drop `where T : Data<T>`. This allows `Data<List<T>>`, `Data<long>`, etc. No special DataList, DataDict classes.

2. **`.` vs `!` navigation convention** — `.` navigates domain properties (DeclaredOnly on concrete type). `!` navigates Data infrastructure (Name, Type, Error, Success). Two rules, deterministic.

3. **DeclaredOnly reflection** — `.` navigation uses `BindingFlags.DeclaredOnly` on the concrete type. If the property isn't declared on the domain type, return empty Data. Never falls back to inherited Data properties via `.`.

4. **`new` keyword for collisions** — Domain types that need Name, Type, or Path use `public new string Name { get; set; }`. This is rare and self-documenting.

5. **Registered navigators per type** — Navigation is NOT baked into GetChildValue via if/else chains. Each type has a navigator registered on the engine (`engine.Navigators.Get(type)`). Navigators for List, Dictionary, Json, CLR reflection ship with runtime. Module authors register their own.

6. **No `__` prefix** — Rejected. The `!` convention handles separation cleanly without polluting C# property names.

7. **No CRTP** — Rejected. Too restrictive — prevents `Data<List<T>>` and other non-self-referential usages.

8. **Scoped inheritance** — Goal, Step, Action stay as they are (program structure). Path, Identity, Settings, Engine, and future handler-created domain types extend Data<T> (value system participants).

9. **Error objects are self-describing** — The Error object carries its own context (BuilderError knows it's from the builder). No need for the container to distinguish error sources.

## What the revised proposal covers

- Summary of the design
- Data<T> without CRTP — two usage patterns (domain types where Value=this, wrapping types where Value=content)
- . vs ! navigation with DeclaredOnly
- Registered navigators (pluggable, per-type, on engine)
- Which types become Data<T> and which don't (scoping)
- The `new` keyword pattern for collisions
- Migration strategy (updated phases)
- Risks and considerations (updated)

## Deliverable
Single file: `Documentation/Runtime2/proposals/everything_is_data.md` (replacement of current content)
