## The work per item is its own action

`foreach` repeats the rest of the step for each element of a collection. What you do with each element is a separate action after it — usually a `call` to a goal:

```plang
- foreach %items% as %thing%, call Show thing=%thing%
```

`foreach %items% as %thing%` binds each element to `%thing%`; `call Show thing=%thing%` is the per-item work, passing the element on. Without `as`, each element is `%item%`; add `with key %sku%` to bind the key or index too.
