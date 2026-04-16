# v6 Plan: Builder Data<T> Unwrapping

Make the builder correctly handle Data<T> wrapped action properties in three areas:
1. `GetTypeName()` — unwrap `Data<T>` to show the inner type name to the LLM
2. `GetValidValues()` — unwrap `Data<T>` to find valid values on the inner type
3. `NormalizeParameterTypes()` — stamp missing parameter types from the action schema

See `Documentation/v0.2/builder-data-t-roadmap.md` for the full roadmap.
