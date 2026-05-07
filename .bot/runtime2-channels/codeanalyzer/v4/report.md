# Codeanalyzer v4 — review of coder v7

**Verdict: PASS**

All v3 findings are correctly resolved. No new bugs, latent traps, or OBP shape issues found.

---

## Prior findings — status

### B1 — `_active` was `static` → FIXED

`private static readonly` → `private readonly`. Because `AsyncLocal<T>` is identified by object reference, a `static` field means all `Channel.Events.@this` instances share one async-local slot. Making it instance-scoped gives each channel its own isolated slot. Fix is correct.

### L1 — `Enter` mutated a shared `HashSet` reference → FIXED

Old pattern: `_active.Value ??= new(); set.Add(bindingId)` — any child task that inherited the same reference and called `Add` would race with the parent, and `Dispose` would remove an id the parent still cares about.

New pattern: copy-on-write. Each `Enter` installs a fresh set containing the parent's bindings plus the new id. `Dispose` restores `_active.Value` to the parent reference, never mutates the inherited set. Trace:

- `Enter("A")`: parent=null → set={"A"}, `_active.Value={"A"}`, Releaser holds (owner, null).
- Nested `Enter("B")`: parent={"A"} → set={"A","B"}, `_active.Value={"A","B"}`, Releaser holds (owner, {"A"}).
- `IsActive("A")` and `IsActive("B")` both true ✓
- Inner Dispose: `_active.Value={"A"}` — "B" gone ✓
- Outer Dispose: `_active.Value=null` ✓

If child tasks inherit and then call `Enter`, each write to `_active.Value` is local to that child's `ExecutionContext`; the parent context is unaffected. The structural trap is closed.

### I1 — `Variables.Snapshot()` ignores overlay → still deferred

Agreed design call: defer until a real caller lands.

---

## OBP shape check — Events/this.cs

1. Public mutable collection with external rules? No — `_list` is private, `Add` owns the lock.
2. Cross-file lock target? No — `_lock` used only inside this type.
3. Same data stored twice across types? No.
4. Allocate-here / mutate-there / clean-up-elsewhere? No — `Enter`/`Releaser` are self-contained.

---

## Test verification

- C# baseline: 2760/2760. After v7: 2760/2760.
- PLang: 205 pass / 6 fixture-fails — identical to v6 baseline; fixture-fails are deliberate test inputs.
