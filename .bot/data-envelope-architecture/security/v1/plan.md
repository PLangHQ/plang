# Security Analysis — data-envelope-architecture branch

## Scope

Security audit of the data-envelope-architecture changes: Data partial class restructure (core, result, navigation, envelope), Engine.Types registry, Variables updates, and ValueNavigators.

## Phase 1: Blue Team (Defensive Audit)

Map the attack surface introduced or modified by this branch:

1. **Data.Envelope.cs** — GZip compress/decompress pipeline, JSON deserialization of untrusted decompressed payloads, RehydrateNestedData recursion, Signature/Verified placeholder properties
2. **Data.cs** — UnwrapJsonElement recursion (deeply nested JSON), Newtonsoft shim via reflection, constructor accepting arbitrary `object?` values
3. **Data.Navigation.cs** — GetChild recursive path traversal, ValueNavigators dispatch chain
4. **Data.Result.cs** — Merge with unbounded list growth, implicit bool conversion
5. **Variables.cs** — ResolveVariablesInPath regex + recursive variable resolution, DeepClone without depth limits, unbounded variable storage
6. **Engine.Types/this.cs** — Runtime Add/Remove of type mappings, Clr() recursive generic parsing, reflection in Name()/ComplexSchemas()
7. **ValueNavigators** — ObjectNavigator reflection-based property access, JsonStringNavigator re-parsing strings as JSON, ListNavigator implicit first-element delegation

## Phase 2: Red Team (Offensive Testing)

For each attack surface, construct exploit scenarios:

1. **Stack overflow via nested JSON** — Craft .pr file with deeply nested JsonElement values that cause recursive UnwrapJsonElement to exhaust the call stack
2. **Stack overflow via RehydrateNestedData** — Craft compressed Data where inner value is a dictionary chain nested 10,000+ levels
3. **Stack overflow via GetChild path** — Craft variable path with 10,000+ dot segments
4. **Zip bomb (partial mitigation check)** — Verify the 100MB limit is correctly enforced, test edge cases at the boundary
5. **Reflection-based property exfiltration** — ObjectNavigator exposes ALL public properties via reflection, including potentially sensitive runtime internals
6. **Variable resolution recursion** — Craft variables where bracket resolution creates infinite loops (`%a[b]%` where b resolves to `a[b]`)
7. **Type registry poisoning** — If untrusted code can call Add(), register extension mappings that confuse downstream type handling
8. **Newtonsoft shim namespace spoofing** — Craft an object in namespace "Newtonsoft.Json.Linq" with a weaponized Value property
9. **Signature bypass** — Verified property is settable without actual crypto; code could check `Verified == true` and be fooled

## Phase 3: Record Findings

Write `security-report.json` with all findings, severity ratings, and proposed fixes. Record learnings in memory.

## Deliverables

- `.bot/data-envelope-architecture/security/v1/plan.md` (this file)
- `.bot/data-envelope-architecture/security/v1/summary.md`
- `.bot/data-envelope-architecture/security/v1/result.md` (detailed findings)
- `.bot/data-envelope-architecture/security/v1/changes.patch`
- `.bot/data-envelope-architecture/security-report.json`
- `.bot/data-envelope-architecture/security/summary.md`
- Session entry in `.bot/data-envelope-architecture/report.json`
