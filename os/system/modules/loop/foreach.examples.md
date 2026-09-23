`loop.foreach` repeats the rest of the step for each item of a collection. Only `Collection` belongs to it — whatever the step does with each item is its own action.

Step text: `foreach %items%, call ProcessItem item=%item%`
Properties: `{"Collection": "%items%"}` — `item=%item%` belongs to the call, not here.

Step text: `foreach %rows%, write out %row%`
Properties: `{"Collection": "%rows%"}`

Step text: `foreach %products% as %product%, call Handle`
Properties: `{"Collection": "%products%", "Item": "%product%"}` — a named item is `Item`.

Step text: `foreach %prices% as %price% with key %sku%, write out "%sku%: %price%"`
Properties: `{"Collection": "%prices%", "Item": "%price%", "Key": "%sku%"}` — a named key is `Key`.
