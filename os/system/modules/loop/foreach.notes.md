Collection — the list or dict to walk · say: `foreach %list%`, `for each %order% in %orders%` · builder: the collection the step names
Item — the variable each element is bound to · say: `as %product%` (else it is %item%) · builder: %item% unless the step names another with `as %name%`
Key — the variable the key or index is bound to · say: `with key %sku%` · builder: only when the step names one

- The work done per element is its own action after loop.foreach in the step's list (usually goal.call), never inside it.
- `name=%item%` after the called goal is an argument of that goal.call, not a foreach property.
