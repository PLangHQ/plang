# test-designer — runtime2-data-share-state

## v1 — Identity-preservation contract for the Data redesign

Translated architect/v1's 6-phase plan into 62 C# tests across 9 new files + 4 PLang `.test.goal` files. Reference-equality is the contract — `Properties`, the three event lists, and `.Value` aliasing pin identity preservation through `As<T>` reads and `Variables.Set` replacements; ref-distinctness pins where a fresh wrapper is required. Resolved two open questions with the user: drop the `Set(string, object?, Type?)` overload entirely, and use `assert type of` syntax in PLang tests (coder will land the assert.type action).

See [v1/summary.md](v1/summary.md) for full details.
