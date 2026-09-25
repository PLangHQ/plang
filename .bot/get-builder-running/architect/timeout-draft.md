# Timeout: one per step — DRAFT, parked

With Ingi, 2026-09-25. **Parked** (separate from the builder work); agreed in full, not yet traced for a plan.

## Why

Two timeouts, one on the step and one inside a module, confuse (Ingi). Today the concept exists twice:
- `timeout.after`, a modifier on any step (`timeout after 5 sec`; `module/action/timeout/after.cs:12`);
- `http.request` / `download` / `upload` each have `TimeoutInSec` (default 30; `http/request.cs:39-41`, used in `http/code/Default.cs:48, :135, :179`); `llm` sets 120 s inside OpenAi's own http call (`llm/code/OpenAi.cs:230`).

## Agreed (Ingi)

1. **The step's timeout is the only timeout.** Every step has one, **30 s by default**; `timeout after X` on the step changes it.
2. **`http` has no timeout property of its own** — `TimeoutInSec` goes; the step controls it. The same for any module that carries its own execution timeout.
3. **A module declares its default when it isn't 30 s** — `llm`: 120 s.
4. **An action declares its own default when it differs from its module** (Ingi: "action need to declare it"):

| action | default | why |
|---|---|---|
| `goal.call` | none | runs a whole goal; its steps carry their own timeouts |
| `loop.foreach` | none | each iteration's steps carry their own |
| `output.ask` | none | waits for a person |
| `timer.sleep` | none | the wait is the action |

   (`goal.return` stays 30 s — one module, different actions, so module-level alone is not enough.)
5. **The builder writes the timeout into the `.pr`** like other frozen defaults, so the `.pr` shows `timeout 30 sec` on an http step — transparent, and the app always runs the same.

## Known limit

A timeout cancels through the cancellation token. Actions that honour it stop (http, llm, sleep); code that ignores it keeps running in the background after the step has returned 408.

## Open (to trace when picked up)

1. How a module/action declares its default (an attribute literal, exact like `[Default(30)]`; "none" spelled explicitly).
2. How the default timeout is applied when a step has no `timeout after` — through the same `timeout.after` wrap, so there is one mechanism.
3. Every other module or action with its own timeout (`signing.verify`'s `TimeoutMs` is probably a signature-validity window, not an execution timeout — check), and every action that must be "none" (listeners, servers, `test.run`, channel waits).
4. Whether the frozen timeout in the `.pr` rides as a modifier row or a step fact.
