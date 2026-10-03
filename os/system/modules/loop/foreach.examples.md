`loop.foreach` repeats the rest of the step for each item of a collection. Only `Collection` belongs to it — whatever the step does with each item is its own action.

Step text: `foreach %items%, call ProcessItem item=%item%`
Properties: `{"Collection": "%items%"}` — `item=%item%` belongs to the call, not here.

Step text: `foreach %rows%, write out %item%`
Properties: `{"Collection": "%rows%"}`

Step text: `foreach %products% as %product%, call Handle`
Properties: `{"Collection": "%products%", "Item": "%product%"}` — a named item is `Item`.

Step text: `foreach %prices% as %price% with key %sku%, write out "%sku%: %price%"`
Properties: `{"Collection": "%prices%", "Item": "%price%", "Key": "%sku%"}` — a named key is `Key`.

Step text: `foreach %goals% in parallel(cpu: 2), call X, write to %task%`
Properties: `{"Collection": "%goals%", "Parallel": {"cpu": 2}}` — `in parallel(cpu: 2)` is `Parallel`; `call X` is its own goal.call; the trailing `write to %task%` keeps the loop's own answer (here its task, since it runs in parallel), its own variable.set.
(then, a later step `wait for %task%` awaits it: `task.wait(Task=%task%)`.)

Step text: `foreach %orders% in parallel, call Ship`
Properties: `{"Collection": "%orders%", "Parallel": true}` — `in parallel` with no count is `Parallel=true` (the default cpu).
