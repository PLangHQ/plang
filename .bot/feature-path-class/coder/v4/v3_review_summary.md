# v3 Review Summary

The v3 review identified 4 remaining OBP violations:

1. **delete.cs** — IgnoreIfNotFound logic lived in the handler instead of Path
2. **exists.cs** — Created `@file` directly instead of delegating to Path
3. **save.cs** — Passed `Context.Engine!` as a parameter to Path.Save (violates "navigate, don't pass")
4. **Tests** — Used `System.IO.File` directly instead of `_fs` (IPLangFileSystem abstraction)

Ingi's direction: "the action class method should just send the object and not all the parameters in the object"

Key design decision from review: **Path should store Engine**, not just `_fs`. This lets Path navigate to anything it needs (OBP rule 2), eliminating the need to pass Engine as a parameter.
