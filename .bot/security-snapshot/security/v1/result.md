# security — snapshot 251 (941df01dd): inherited bindings

Probes (tests only): PLang.Tests/Runtime/App/Events/InheritedBindingProbeTests.cs (6 green),
InheritedHandlerResolutionProbeTests.cs (2 green, 1 RED by design).

- 2 child `%!app.event%` and its save hold own bindings only: green.
- 3 no path reaches a binding's actor/scope; `before`/`after` of an event are not navigable; Remove by method hop refused: green.
- AppTooDeep ends a handler that makes sub apps: green.
- Handler's own goal is found in the maker's app (Actor.App): green.
- RED: a goal the handler CALLS is selected by goal.call in the child's goal list, so a child's own loaded goal of the same name
  (`sub/Helper`) answers in place of the maker's (`Helper`). Finding S-1, Medium (policy handler runs child code), fix: resolve
  the handler's whole call chain in the goal's own app, or run a maker's handler under the maker's goal list.
