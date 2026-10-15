# returns

Traces to: the conversation with Ingi on 2026-10-15, logged in `.bot/kind-is-type/architect/summary.md` as decision 801. Chain: idea → conversation → summary → this plan → code (`start.goal` and the tests it runs). Built after kind-is-type (Ingi: "yes").

## Why

A goal returns one value. To return two, a developer builds a dict and keys it, which is the envelope smell: a container whose keys exist only to carry. Ingi: "I want to return 2 object, so how do you do it. well you create a dict and key it, but this is wrong with obp. you can return multiple return, the object you get back is just the default, but return.list gives you all … then for instance threads can give us back returns with not problem". A `Data` already carries its name and its type, so several returns are a list of them, with no wrapper.

## What the developer gets

```plang
GetCustomer
- return %user%, %order%                  → two returns, named user and order, each with its type

Start
- call GetCustomer, write to %c%          → %c% is the default: the first return, %user%
- call GetCustomer, write to %u%, %o%     → %u% is %user%, %o% is %order%, by position
- call GetCustomer, write to %c%
- write out %c!list%                      → every return, in order: user, order
- wait for %task%, write to %result%
- write out %result!list%                 → the task's returns
- call Fetch for each %urls% in parallel, write to %pages%
- write out %pages!list%                  → one return per run, in the order of %urls%
```

## The rulings (Ingi, 2026-10-15)

1. **A goal returns one or more values.** Each keeps its own name and type; no dict carries them.
2. **The value you get is the default, the first return.** `%x%` and `%!data%` are it, as today.
3. **All the returns are the binding's own fact, `%x!list%`** (Ingi: "yes"), beside `%x!type%`; never a member of the default value, so a default that is itself a dict or a list doesn't clash.
4. **`write to %a%, %b%` binds by position** (Ingi: "yes"): the first return to the first variable, and so on.
5. **Threads and tasks give their returns the same way:** a task's answer carries its returns; a parallel call's answer carries one return per run, in order.

## Stages

Each stage ends with its tests built and run; each test goes red without its change. Each stage's shape comes to the architect before it's built.

### 0. Trace: how a return travels today

`goal.return` takes one `Data` (`module/goal/return.cs:9`) and marks it `Returned` with a depth (`:18–20`); the steps stop on it, and the goal folds the depth (`goal/this.cs:399–405`); `write to %x%` becomes a `variable.set` from `%!data%` in the built `.pr`; `task.wait` answers the task's result (`module/task/wait.cs:20`, `task/this.cs:38`); a parallel `goal.call` (`module/goal/call.cs:33`, `:83`) gathers its runs. The trace lists each place a goal's answer is made, carried and read, and where the returns would hang (`data`'s own facts, the `!` door).

### 1. A goal returns several values

```
- return %user%, %order%                  → the answer's value is %user%; %x!list% is [user, order]
- return %user%                           → as today; %x!list% is [user]
```

### 2. Writing several returns

```
- call GetCustomer, write to %u%, %o%     → by position
- call GetCustomer, write to %u%, %o%, %z%   (two returns)   → an error naming the third, never a silent null
```

The builder writes `write to %a%, %b%` as what the run binds by position; its teaching and the decider pins move (the teaching-edit rule).

### 3. Tasks and parallel calls

```
- wait for %task%, write to %result%      → %result!list% is the task's returns
- call Fetch for each %urls% in parallel, write to %pages%   → %pages!list%: one per run, in order
```

## Demolition

Nothing in the code builds the dict pattern for a developer, so nothing is removed. **Stays:** a single `return %x%` exactly as today; `%!data%` as the default; return depth.

## What tests can't show (checks the coder runs and reports)

- The builder builds `return %a%, %b%` and `write to %a%, %b%` 5/5 (fresh, cache skipped).
- `data`'s new fact re-pins the surface golden under 801 (a locked addition, Ingi's yes).
- Every stage: the gate by name; PLNG001/002 at 0; the plang suite no worse than runtime2's.

## OBP validation

| new surface | check | result |
|---|---|---|
| several returns | a list of `Data`, each named and typed; no container type beside them | to verify at stage 1 |
| `%x!list%` | the binding's own fact, one door, beside `!type`; the default value's members untouched | to verify at stage 1 |
| positional `write to` | the step's write-to list (`Action.Return`) binds in order; no name matching | to verify at stage 2 |
| tasks and parallel | one way to carry returns, whatever produced them | to verify at stage 3 |

The coder owns the code shape; the sketches here are intent.
