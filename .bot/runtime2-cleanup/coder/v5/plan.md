# Stage 5 — coder plan (`getstatic-shim-drop`)

Delete the one-line `internal GetStatic(string)` shim on App and migrate
its single caller in Actor/Context to `App.Statics.GetBag(key)`.

## Files

- `PLang/App/this.cs` — delete the doc comment block + the line `internal ConcurrentDictionary<string, object?> GetStatic(string key) => Statics.GetBag(key);` (was line 115).
- `PLang/App/Actor/Context/this.cs:248` — `"app" => App.GetStatic(key)` → `"app" => App.Statics.GetBag(key)`.

## Verification

- `grep -n "GetStatic\b" PLang/` → 0
- C# 2755/2755; PLang 199/199; build clean.
