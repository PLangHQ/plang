Collection — the list or dict to walk · say: `foreach %list%`, `for each %order% in %orders%` · builder: the collection the step names
Item — the variable each element is bound to · say: `as %product%` (else it is %item%) · ask: which of the step's variables does it bind each element to (`as %name%`)? · builder: %item% unless the step names another with `as %name%`
Key — the variable the key or index is bound to · say: `with key %sku%` · ask: which of the step's variables does it bind the key to (`with key %name%`)? · builder: only when the step names one
Returns — a summary of the loop: `{count, complete}` — how many elements it ran over, and whether it finished (false if cancelled). The work per element is its own action, so there is usually nothing to write the summary to.

- The work done per element is its own action after loop.foreach in the step's list (usually goal.call), never inside it.
- `name=%item%` after the called goal is an argument of that goal.call, not a foreach property.
