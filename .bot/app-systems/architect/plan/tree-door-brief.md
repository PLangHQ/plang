# Parked brief: running a verb on any node of the app tree (after app-systems)

Ingi, 2026-09-27, while settling formats: "plang needs to understand structure of app to do this, he needs to see the tree and see what properties/methods/classes are under there, so that he can then map plang.run("app.type.text.kind.list", "add", ".foo")". Then: "it's class:"add" and property: [{}]", and "sorry format, not kind".

## The idea

A step like `- add format .foo as text, mime text/x-foo` maps to one action row with the same shape every action has:

```
{ node: "app.type.text.format.list", class: "add", property: [ {name: "extension", value: ".foo"}, {name: "mime", value: "text/x-foo"} ] }
```

- The runtime navigates to the node (read-only, as navigation already works), then runs the action class on it.
- A node's verbs are **action classes** with typed `Data<T>` properties, like module actions (`file.read`). So the generator, PLNG001, the missing-parameter guard and the catalog shape all apply.
- `%!app…%` stays read-only for navigation. A change always goes through this action, on its own step.

## Open questions for when it's picked up

1. **Where the action classes live.** Can an action class live on any node (e.g. `app/type/text/format/list/add.cs` → class `add` on `%!app.type.text.format.list%`), with a module being just a node that holds actions? Or does the tree door always point into a module? (Ingi: "I dont know about your question"; not needed yet.)
2. **Picking the node.** The builder can't see the whole tree, so it needs two stages, like modules today: pick the node, then show that node's action classes. How does the LLM learn that "a format" lives at `app.type.<t>.format`? Either nodes carry short descriptions the decider scores against, or the concepts are taught.
3. **Permission.** A mutating verb on the app (adding formats, types, modules) must say who may run it (system only? any actor?), the way paths gate their verbs.
4. **Modules vs the tree.** If modules are nodes that hold actions, a module action and a tree verb shouldn't be two ways to reach one thing.

## First step, when picked up

The smallest proof: one tree action (`add` on `app.type.<t>.format.list`), the node-picking stage for it, and one eval.
