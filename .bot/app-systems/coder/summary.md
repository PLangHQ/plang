# coder — app-systems

**Version:** v6

## What this is
app-systems makes every `app.X` the type X (a generic `type<X>` over its concept's `list<X>`), so the
plang path, the C# path and the file path agree. The architect's plan
(`.bot/app-systems/architect/plan.md`) lays it out in 13 stages. v1–v5 covered stages 0, 1, 3, 4 and 5;
v6 is stage 6, the reference: one definition of what `%…%` is.

## What was done
- **v1–v5:** see `v1/result.md` … `v5/result.md`.
- **v6, stage 6** (details, commits, the eval and the plang-visible changes in `v6/result.md`):
  - `app.variable` moved to `app.type.item.variable`.
  - A variable is its `Text` and its `Code`: hops `variable`, `property`, `index`, `method`, each
    reading, running and writing its own piece. It reads with `Start(context)` and writes with
    `Set(value, context)`.
  - `app.type.item.variable.parser` is the only definition of a reference. The old path tokenizer,
    Data's walker, the method switch, and nine regexes and hand scans are gone. Their callers (Formal,
    step Cover/Scope, pick, debug, text's render) now use the parser.
  - Text owns its methods (`replace`, `grep`, …), reachable only when marked `[LlmBuilder]`.
  - Every value names its variables (`item.Variable`). A stored `.pr` row writes them (`"variable"`),
    so loading never parses; an authored marked row without them is PrFormatOutdated. The 16 marked
    `.pr` files were rewritten.
  - The variable store takes root names only, and `%setting.X%` is gone.
  - The Python twin (`tools/decider/variables.py`) mirrors the parser and the lists.
- **Results:**
  - The six suites are at or under the baseline by name; the twins are equal; `plang --test` is
    7/0/317.
  - Eval: 58/58 right on the golden goals in the end. The builder's goals: 9 first try, 1 retried,
    3 refused, all nano's answer shape. The prompts are provably unchanged (see `v6/result.md`).
- **Waiting on Ingi:** the kind faces (from v5).
- **Next:** stage 7.

## Code example
The same variable reads and writes, and each hop does its one step:
```csharp
var v = new parser.@this("%user.address[idx].city%").Read(0)!;
await v.Set("Oslo", context);      // user, .address, [idx] reached; .city writes itself
var city = await v.Start(context); // every hop in order → "Oslo"
```
