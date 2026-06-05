# Test coverage — Collections are Data

For the test-designer. The coverage matrix (stage × surface), the failure/mutation matrix (what a missing test would let through), and the new-surfaces inventory.

## Coverage matrix

| Surface | Stage | Layer | Must assert |
|---|---|---|---|
| `dict` Get/Keys/Has/truthy | 1 | C# unit | key lookup, missing key, empty-dict falsy |
| `dict` serialize `{}` | 1 | C# unit | named entries; nested dict; record → `{}` |
| `Dictionary` navigator collapse | 1 | integration | `%u.name%`, nested `%a.b.c%`, `count`-key precedence |
| `NormalizeObject` → `dict` | 1 | C# unit | a domain record (`permission`) normalizes to named `Data` |
| writer `[]`-vs-`{}` by type | 1, 3 | C# unit | `dict`→`{}`, `List<data>`→`[]`, no property-bag arm |
| `Variables.Set` rebind (both raw branches) | 2 | C# unit + integration | rebind not mutate; subscriber-carry; `:199` frame branch |
| dot-path `SnapshotClone` removed | 2 | integration | dot-path `set` independence via rebind, not clone |
| `UnwrapJsonArray` → `List<data>` | 3 | C# unit | elements are `Data`, not raw |
| `Element` raw branch + `WrapItem` gone | 3 | C# unit | navigation returns the element `Data` directly |
| signed `Data` survives in a list | 3 | integration | **sign→add→save `.plang`→read→verify `%list[0]%`** (load-bearing, wire round-trip) |
| `.json` of a signed list is bare | 3 | integration | save signed list to `.json` → value present, signature absent (signatures are wire-only) |
| `Conversion` element-unwrap | 3 | C# unit | coerce `List<data>` → typed `List<T>` |
| typed compare on the type | 4 | C# unit | number/date/duration/text order; nulls last; two-type sort throws; equality-only types reject sort; one path for `if` and `sort` |
| `where` on `dict`+`list` | 5 | integration | dict keep/drop; list filter; same syntax both cardinalities |
| `sort`/`group` list-only | 5 | integration | `group` buckets are `Data<List<data>>` |
| `item` apex + `is` query | 6 | integration | `is item`/`is dict`/`is number`; IS-A through the narrow |
| passthrough no-parse | 3 | integration | read-json→write-json, `RawUntouched` stays true; `Data.Load()` short-circuits (row Q) |
| per-element `ILoadable` in a list | 3 | integration | `Data.Load()` walks the list, loads a signed image at `%list[2]%` before serialize |

## Failure / mutation matrix

What each test catches — and what slips through if it's missing.

| Mutation | Caught by | Slips through without it |
|---|---|---|
| element stored raw instead of `Data` | sign→add→verify `%list[0]%` | F1 returns silently; signature lost on the element |
| `set` mutates in place (revert rebind) | `set %x%="b"` → `%list[0]%`=="a" | stored values rewrite underfoot; the worst inside `:199` flows |
| writer keeps property-bag arm | array round-trip after Stage 3 | `[]` emitted as `{}` once arrays are `List<data>` |
| `Materialize` json branch stays raw | `%content.name%` on a read object | nav fails or re-wraps; passthrough parses unnecessarily |
| `Data.Load()` doesn't short-circuit | passthrough `RawUntouched` stays true | serialize chokepoint parses every passthrough value |
| narrow loses IS-A | `is item` after narrow to `dict` | apex predicate breaks; `where` on apex stops erroring |
| compare path diverges (sort vs `if`) | `if a.age>b.age` agrees with `sort by "age"` | three compare paths drift; sort-by-field wrong |
| sort on equality-only type silently orders | `sort` on a list of `dict` throws | a meaningless order ships instead of a clear error |

For the load-bearing proof, mutation-test it: with Stage 3 in, revert the `Element` raw-branch deletion and confirm the verify test fails. Announce the mutation first per CLAUDE.md.

## New surfaces inventory

Surfaces that don't exist yet and need first tests:

- `app/type/dict/` — value type, navigation, serializer, truthiness.
- `app.type.list.@this` value type (promoted into the `app.type.list` slot; the registry renamed to `app.type.catalog.@this`) — navigation moved off `navigator/List.cs`.
- `where` action — new, on `dict` and `list`.
- `item` apex registration + `Is(string)` overload (if added).
- typed compare entry point (the adapter that takes two element `Data`, picks the type, compares).

## You own the final shape

The matrix is the coverage contract — names, fixtures, and exact assertions are yours.
