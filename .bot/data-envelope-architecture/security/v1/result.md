# Security Audit Results — data-envelope-architecture

## Attack Surface Map

```
EXTERNAL INPUT
     │
     ├── .pr file parameters ──────────► Data constructor (Data.cs:109)
     │                                        │
     │                                        ├── UnwrapJsonElement ← FINDING 1: no depth limit
     │                                        └── UnwrapNewtonsoftToken ← FINDING 8: namespace spoof
     │
     ├── Transport channel ────────────► Decompress (Data.Envelope.cs:129)
     │                                        │
     │                                        ├── GZipDecompress ← 100MB limit (OK)
     │                                        ├── JsonSerializer.Deserialize ← no depth limit
     │                                        └── RehydrateNestedData ← FINDING 2: no depth limit
     │
     ├── variable/set action ──────────► Variables.Set (Variables.cs:49)
     │                                        │
     │                                        └── No ! prefix guard ← FINDING 4: system var overwrite
     │
     ├── %var.path% navigation ────────► GetChild (Data.Navigation.cs:14)
     │                                        │
     │                                        ├── Recursion ← FINDING 3: no depth limit
     │                                        └── ValueNavigators.Navigate
     │                                              ├── ObjectNavigator ← FINDING 5: reflection
     │                                              ├── JsonStringNavigator ← FINDING 6: no size limit
     │                                              └── ListNavigator ← implicit first chains
     │
     ├── fromJson action ──────────────► UnwrapJsonElement (fromJson.cs:26)
     │                                        │
     │                                        └── Same recursion as FINDING 1 (duplicate code)
     │
     └── Type name resolution ─────────► Clr() (Types/this.cs:407)
                                              │
                                              └── Recursive generic parsing ← FINDING 10: no depth limit
```

## Findings Detail

### FINDING 1 — Unbounded JSON recursion in UnwrapJsonElement
**Severity: HIGH | Category: Resource Exhaustion**

**Location:** `Data.cs:208-278`, `fromJson.cs:26-41`, `JsonStringNavigator.cs:39-71`

Three separate copies of the same recursive JSON unwrapping pattern exist:

```csharp
// Data.cs:220-221 — called from Data constructor (line 112)
JsonValueKind.Object => UnwrapJsonObject(element),  // recurses
JsonValueKind.Array => UnwrapJsonArray(element),     // recurses

// UnwrapJsonObject at Data.cs:260-268
foreach (var prop in element.EnumerateObject())
    dict[prop.Name] = UnwrapJsonElement(prop.Value);  // no depth check

// fromJson.cs:30-31 — separate copy, same pattern
JsonValueKind.Object => element.EnumerateObject()
    .ToDictionary(p => p.Name, p => UnwrapJsonElement(p.Value)),
```

No depth counter exists in any of these. Stack overflow at ~1000-2000 CLR frames.

**Note:** System.Text.Json's `JsonDocument.Parse` defaults to MaxDepth=64 for initial parsing, but `UnwrapJsonElement` processes the already-parsed `JsonElement` tree, which can have depth limited only by the parser. The real risk is the recursive unwrap, not the parse.

---

### FINDING 2 — Unbounded recursion in RehydrateNestedData
**Severity: HIGH | Category: Resource Exhaustion**

**Location:** `Data.Envelope.cs:184-200`

```csharp
private static void RehydrateNestedData(Data data)
{
    if (data.Value is Dictionary<string, object?> dict && dict.ContainsKey("value"))
    {
        // ... reconstruct inner Data ...
        var inner = new Data(name, value, type);
        RehydrateNestedData(inner);    // LINE 195: recurse, no depth limit
        data.SetValueDirect(inner);
    }
}
```

This is the most dangerous finding because it processes **decompressed transport data** — the exact boundary where untrusted external input enters. An attacker sends a small compressed payload (GZip is very effective on repetitive JSON structures) that decompresses to deeply nested dictionary chains.

**Compression ratio attack:** The JSON `{"name":"a","value":{"name":"a","value":...}}` repeated 5000 times is ~90KB uncompressed but compresses to ~2KB with GZip. Well under the 100MB limit, but causes stack overflow.

---

### FINDING 3 — Unbounded recursion in GetChild navigation
**Severity: HIGH | Category: Resource Exhaustion**

**Location:** `Data.Navigation.cs:14-65`

```csharp
public Data? GetChild(string path)
{
    // ... parse segment ...
    return child.GetChild(remaining);  // LINE 64: recurse, no depth limit
}
```

Combined with `ListNavigator.cs:41`:
```csharp
// Implicit first: delegate to first element's navigator
return ValueNavigators.Navigate(list[0]!, key);
```

This creates two amplification paths:
1. Direct: `%var.a.b.c.d...%` with thousands of dot segments
2. Amplified: nested lists where each level delegates to first element

---

### FINDING 4 — System variable overwrite via Variables.Set()
**Severity: HIGH | Category: Injection / System Hijacking**

**Location:** `Variables.cs:49-64`, `variable/set.cs:15`, `PLangContext.cs:120-126`

System variables are registered with `!` prefix at context initialization:
```csharp
// PLangContext.cs:120-126
ms.Put(new Data("!engine", Engine));
ms.Put(new Data("!context", this));
ms.Put(new Data("!memoryStack", ms));
ms.Put(new Data("!fileSystem", Engine.FileSystem));
```

But `Variables.Set()` has **no guard**:
```csharp
// Variables.cs:49-64
public void Set(string name, object? value, Type? type = null)
{
    name = CleanName(name);    // strips % only, keeps !
    if (_variables.TryGetValue(name, out var existing))
    {
        existing.Value = value;  // overwrites system variable value
    }
    // ...
}
```

And `variable/set` passes Name directly:
```csharp
// set.cs:15
Context.Variables.Set(Name, Value, ...);
```

**Attack:** A crafted .pr file with `set %!fileSystem% = null` nullifies the filesystem abstraction. All subsequent file operations throw NullReferenceException. Or replace with a malicious implementation for data exfiltration.

**Mitigation should be at Variables.Set() level**, not at the action level, because multiple code paths call Set() (list/set, loop/foreach, etc.).

---

### FINDING 5 — ObjectNavigator reflection exposes internal state
**Severity: MEDIUM | Category: Information Disclosure / Privilege Escalation**

**Location:** `ObjectNavigator.cs:12-18`

```csharp
public object? GetProperty(object value, string key)
{
    var prop = value.GetType().GetProperty(key,
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    return prop?.GetValue(value);
}
```

If a Data object's Value is an Engine instance (stored as `!engine` in Variables), navigation exposes:
- `Engine.FileSystem` — full file system access
- `Engine.Libraries` — handler registry
- `Engine.Goals` — goal execution
- `Engine.System` / `Engine.Service` — higher-trust actors
- `Engine.Variables` — User actor's variable store

The `!` prefix prevents enumeration via GetNames/GetAll, but does NOT prevent direct access if the variable name is known. Whether `%!engine.fileSystem%` resolves depends on whether the `!` is stripped by CleanName — it is NOT stripped (CleanName only strips `%`), so `Get("!engine")` returns the system variable.

---

### FINDING 6 — JsonStringNavigator parses without size limit
**Severity: MEDIUM | Category: Resource Exhaustion**

**Location:** `JsonStringNavigator.cs:20-37`

```csharp
public object? GetProperty(object value, string key)
{
    if (value is not string str) return null;
    // No size check on str.Length
    var doc = JsonDocument.Parse(str);    // parses entire string
    var parsed = UnwrapElement(doc.RootElement);  // allocates entire structure
    return ValueNavigators.Navigate(parsed, key);
}
```

Every navigation access re-parses the full JSON string. No caching.

---

### FINDING 7 — Variable resolution cycle in bracket paths
**Severity: MEDIUM | Category: Resource Exhaustion**

**Location:** `Variables.cs:207-215`

```csharp
private string ResolveVariablesInPath(string path)
{
    return Regex.Replace(path, @"\[([^\]\d][^\]]*)\]", match =>
    {
        var varName = match.Groups[1].Value;
        var resolved = GetValue(varName);   // calls Get() → may trigger ResolveVariablesInPath again
        return resolved != null ? $"[{resolved}]" : match.Value;
    });
}
```

`GetValue` calls `Get()` (line 107), which calls `ResolveVariablesInPath` again if the path contains `[` (line 77-78). Mutual recursion with no cycle detection.

---

### FINDING 8 — Newtonsoft namespace spoofing
**Severity: MEDIUM | Category: Code Execution**

**Location:** `Data.cs:228-258`

```csharp
// Line 228: only checks namespace string
if (value != null && value.GetType().Namespace == "Newtonsoft.Json.Linq")
    return UnwrapNewtonsoftToken(value);

// Line 248: calls attacker-controlled property getter
var underlying = value.GetType().GetProperty("Value")?.GetValue(value);
```

Requires attacker to load a custom assembly (via library.load), making this a chained attack. If library.load is available, the attacker has RCE already — but this shim lowers the barrier by providing an unexpected reflection gadget.

---

### FINDING 9 — Verified property is a settable bool with no crypto
**Severity: MEDIUM | Category: Injection (Future)**

**Location:** `Data.Envelope.cs:37-42`

Not currently exploitable. No production code checks `Verified`. But the property exists as a public settable bool that any code can set to `true`. A time bomb if future code trusts it.

---

### FINDING 10 — Recursive generic type parsing in Clr()
**Severity: MEDIUM | Category: Resource Exhaustion**

**Location:** `Types/this.cs:407-454`

```csharp
public System.Type? Clr(string plangName)
{
    if (plangName.StartsWith("list<", ...) && plangName.EndsWith(">"))
    {
        var innerTypeName = plangName[5..^1];
        var innerType = Clr(innerTypeName) ?? typeof(object);  // recurse
        return typeof(List<>).MakeGenericType(innerType);
    }
}
```

---

### FINDING 11 — Merge unbounded list growth
**Severity: LOW**

### FINDING 12 — DeepClone without depth limits
**Severity: LOW**

(Details in security-report.json)

---

## Priority Recommendations

### Must-fix before merge (HIGH severity)

1. **Add depth limits to all recursive unwrap/rehydrate/navigate methods**
   - UnwrapJsonElement: max 128
   - RehydrateNestedData: max 100
   - GetChild: max 100
   - Clr(): max 20

2. **Guard Variables.Set() against ! prefix**
   ```csharp
   if (name.StartsWith("!"))
       throw new InvalidOperationException($"System variable '{name}' is read-only");
   ```

### Should-fix (MEDIUM severity)

3. Add size limit to JsonStringNavigator (10MB max string)
4. Add property blocklist to ObjectNavigator
5. Add cycle detection to ResolveVariablesInPath
6. Verify Newtonsoft assembly identity, not just namespace
7. Make Verified { get; internal set; } or remove it
8. Share UnwrapJsonElement between Data.cs and fromJson.cs (eliminate duplicate)

### Monitor (LOW severity)

9. Merge list size limits
10. DeepClone depth awareness
