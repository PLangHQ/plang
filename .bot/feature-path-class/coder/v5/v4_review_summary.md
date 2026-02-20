# v4 Review Summary

The v4 review identified one core OBP violation across all file handlers:

1. **Handlers decompose action record properties into method parameters** — e.g., `Path.Delete(Recursive, IgnoreIfNotFound)` instead of `Path.Delete(this)`. This violates OBP rule 2 ("navigate, don't pass"). The reviewer specified the exact pattern for each handler: `Path.Delete(this)`, `Source.Copy(this)`, `Path.List(this)`, `Source.Move(this)`, `Path.Save(this)`.

2. **Rename `AsFile()` to `Exists()`** — The method wraps a path as a `@file` object for the exists handler. `Exists()` is clearer about intent.

3. **Minimize `System.IO` in test constructors** — Keep only the 2 bootstrap lines (temp dir creation before `_fs` exists).
