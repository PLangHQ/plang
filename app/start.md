# app

`app` is the root of a running program. The [console](console/start.md) starts
it: it builds an `app` around one entry goal and calls `start()`. From `app`,
every concept is reachable, and owned once.

```csharp
app(Data<goal> goal)            // the entry goal — what this program runs
  → goal.start()
```

`app` is handed one goal — the entry — and starting `app` is starting that goal.
Nothing constructs a sibling; it asks `app`.

## the console starts it

The process boundary is the [console](console/start.md). It reads the entry
goal's name from the command line (default `"start"`), builds the `app`, and
starts it:

```csharp
console.start(input)
  → new app(new goal { name = input ?? "start" }).start()
```

A goal is named **or** located: born from a `name` (a reference, resolved
against the program) or from a `prPath` (a compiled `.pr` on disk). The console
names the entry; a `call` names its target; the loader hands over a `prPath`.
The goal loads itself from whichever it was given — there is no separate file
step in front of it.

## execution chain

```
console.start(input)
 └─ app.start()                  start the entry goal
     └─ goal.start()             run the steps — start the list
         └─ step.list.start()    the list owns the loop; stops at the first error
             └─ step.start()     run the actions — start the list
                 └─ action.list.start()   the list owns the loop; stops at the first error
                     └─ action.start()    the leaf — does the work
```

Each list level checks the returned `Data`: the moment a `start()` comes back
`!Success`, the loop stops and returns that error as-is. Behavior flows down,
the error flows back up — nobody inspects it on the way.

Every `start()` asks one question: *what does this want to do?* A goal wants to
run its steps — that's the **list** — so it starts the list. See [obp](obp/start.md).

## owns

| concept | `Data<T>` | role |
|---------|-----------|------|
| [goal](goal/start.md) | `Data<text>` name **or** `Data<path>` prPath | named step sequence; the entry, and every `call` target |
| [type](type/start.md) | `Data<text>` name | the value kinds |
| [channel](channel/start.md) | `Data<text>` name | where output goes |
| [identity](identity/start.md) | `Data<text>` name, `Data<text>` publicKey | who is running |
| [signing](signing/start.md) | `Data<text>` data → `Data<text>` signature | data integrity |
| [llm](llm/start.md) | `Data<text>` prompt → `Data<text>` response | language model |
| [translate](translate/start.md) | `Data<item>` value, `Data<type>` target | format conversion |
| [error](error/start.md) | `Data<text>` message | what to do when things fail |
| [warning](warning/start.md) | `Data<text>` message | what to flag without stopping |

`app` is the program. One run of it is a [context](context/start.md) — the
per-run state (variables, current goal/step). Context is navigated through
`IContext`, never passed.

## OBP rules

- **Ownership is here.** Every concept lives once, in `app`. No concept constructs
  a sibling — it receives what it needs.
- **`app` is given its entry.** The console hands `app` one goal and starts it.
  `app` does not look for a file or scan a folder — it runs the goal it was given.
- **Data in, Data out.** Every method boundary uses `Data<T>`. No raw `string`,
  `int`, or `bool` crosses a method signature (the console's CLI string is the
  one perimeter exception, wrapped before it crosses into `app`).
- **Behavior flows down.** `app` starts `goal`, `goal` starts `step`, `step`
  starts `action`. Nothing flows back up but the result.
- **The list owns the loop.** `step.list.start()`, `action.list.start()` — the
  list iterates and short-circuits on error; the element does one thing.

## source

`app/start.cs`
