# v1 Plan — Implement Settings method bodies

## Goal
Make 15 red tests green by implementing the 6 `NotImplementedException` method bodies.

## Files
1. **Scope.cs** — `Get`, `Set`, `Contains` — thin ConcurrentDictionary wrappers
2. **this.cs** — `Resolve<T>` (walk context→parent→defaults→classDefault), `Set` (isDefault→Defaults, else→context.SettingsScope), `For<T>` (derive module prefix from namespace)
3. **ModuleView.cs** — `Resolve<TValue>` — prefix property name, delegate to Settings.Resolve

## Approach
Tests are the spec. No architectural decisions needed — scaffolder already made them.
