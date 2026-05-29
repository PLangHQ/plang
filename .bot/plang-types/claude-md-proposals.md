# CLAUDE.md proposals — `plang-types` branch

## architect — v1 — 2026-05-28
**Target:** /CLAUDE.md
**Why:** The OBP Smell Checklist embedded in the project CLAUDE.md catches collection-shape smells, ownership smells, flat-copy smells — but not the "courier opens the package" smell that Ingi crystallized on 2026-05-28 ("you clearly understand never to touch the Data value until the last possible moment"). The principle now lives in `Documentation/v0.2/object_pattern_formal.md` as new Rule #9 ("Only leaves touch `Data.Value`"). The checklist should have a one-line entry that points there, so a bot scanning for smells in C# catches mid-pipeline `data.Value as X` / `is X` switches without having to know the canonical rule by heart.

**Proposed change:** Add a new item to the OBP Shape Smells list in `/CLAUDE.md`, after item 6:

```markdown
7. **Courier reaches into `Data.Value`.** A relay layer (a handler that forwards Data, variable memory, callstack, channel routing, signing, the wire envelope) does `data.Value as X` or `if (data.Value is X)` to branch on the contained value. The code is opening a package that should stay closed mid-flight. Only leaf actions (handlers declaring a typed `Data<T>` parameter) and leaf serializers (the value's own `IWireWritable.WriteTo`) get to read `.Value`. Detection: grep for `\.Value (is|as|switch)` outside files that declare `Data<T>` parameters. Full rule: `Documentation/v0.2/object_pattern_formal.md` Rule #9 — "Only leaves touch `Data.Value`".
```
