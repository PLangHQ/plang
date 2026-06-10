# Coder summary — compare-redesign

- **Version**: v6 (continued) — design session + speed work, 2026-06-10
- **What this is**: This branch carries the born-typed value work (Stage 9) on
  top of the compare-redesign stages. The latest session pivoted: Ingi stopped
  the consumer-patching approach ("you are unwrapping way too much") and we
  held a step-by-step design session that produced the agreed target model for
  Data/Value. Separately, build/test iteration speed was measured and fixed.

## What was done (latest state)

1. **`data-value-model.md`** (`.bot/compare-redesign/coder/`) — THE document.
   The agreed Data/Value model from the session with Ingi: Data owns
   name/Type/properties and the type instance IS the value; `Value()` is fully
   async and rebinds Type on the type's own answer (file → dict); `Peek()` vs
   `Value()` = no-parse vs parse; three rungs for C# types (own item /
   item|kind / never-in-Data); `type.@this` becomes an item; Assembly never
   rides in Data (take-over API); nested Data is replaced by schema layers
   (`data|signature|encryption|compress`, future branch); Materialize dies.
   **Architect is reading this now.** Open items fenced at the doc's end.
2. **The half-done door retype was reverted** (`PLang/app/data/this.cs` back to
   HEAD) — disposable per the doc. Tree compiles.
3. **`dev.sh` + `build-speed-report.md`** — measured and fixed iteration
   speed: `dotnet run --project PLang.Tests` (90s+/call) banned; warm builds
   1.1s no-change / 4.6s PLang-edit / 31s test-edit (analyzers off,
   consistently — flag flips thrash incrementality); filtered test runs ~3s
   via the binary directly. `./dev.sh full` is the analyzers-ON pre-commit
   gate (PLNG001/PLNG002 + TUnit warnings). After-idle 2-min stall mitigated
   by `./dev.sh warm`. CLAUDE.md proposal appended to claude-md-proposals.md.
   Optional next: split PLang.Tests per area (31s → ~5s) — awaiting Ingi.

## Test state

- plang suite: **322/324 green** (2 deliberate skips).
- C# suite: ~5 known fails, all on the now-disposable stage9 consumer-patching
  path (PrAction trio, Data_PropertyAccess, TypedSnapshot edit-resume). Do NOT
  patch them further — the value-model rebuild supersedes that code path.
  Earlier in the session that path went 69 → 5 before being declared
  disposable; its useful residue (test-contract fixes, Conversion.cs lowering,
  navigation text gate) is committed.
- Watch item: intermittent segfault AFTER full C# results print (results
  valid, pre-existing).

## Code example (the agreed model, from the doc)

```csharp
class Data {
    item type;                       // the typed instance — IT IS THE VALUE
    async ValueTask<item> Value() {
        this.type = await type.Value();   // type answers — maybe as a DIFFERENT type (file→dict)
        return type;
    }
}
```

## Next

Architect reviews `data-value-model.md`; context will be cleared after. The
next coder session starts from that doc (NOT from the stage9 patching diff),
uses `./dev.sh` for all build/test (see build-speed-report.md), and brings the
doc's fenced open items (type-chain reconciliation, `Value<T>` vs `As<T>`) to
Ingi before deciding anything.
