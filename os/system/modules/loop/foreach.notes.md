Collection — the list or dict to walk.
Item — the variable each element is bound to; %item% unless the step names another (`as %product%`).
Key — the variable the key or index is bound to, only when the step names one (`with key %sku%`).

- The work done per element is its own action after loop.foreach in the step's list (usually goal.call), never inside it.
- `name=%item%` after the called goal is an argument of that goal.call, not a foreach property.
