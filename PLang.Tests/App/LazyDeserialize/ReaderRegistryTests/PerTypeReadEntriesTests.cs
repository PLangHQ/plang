using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// After Stage 1's consolidation, every type that used to read through one of
// the incumbents (per-family `Convert`, `FromWire`, `path.JsonConverter`,
// `type.json`, the per-type `JsonConverter<T>` set) has a `Read` entry in
// the registry. These are the existence pins — round-trip parity sits in
// TypeOwnedReadParityTests.
public class PerTypeReadEntriesTests
{
    // Independent #3 — the registry-level "the (object, json) entry
    // exists" probe. Architect 829785fbe sets shape-based dispatch:
    // json/xml/yaml live under `object`; csv/xlsx under the new `table`.
    [Test] public async Task Reader_Of_ObjectJson_ReturnsDelegate() { throw new System.NotImplementedException("not implemented"); }

    // The new `table` type's primary kind. `(table, csv)` lands in-branch;
    // `(table, xlsx)` is a follow-on (binary, needs a library) — until
    // then, a .xlsx stamps `{table, xlsx}` and rides as raw bytes.
    [Test] public async Task Reader_Of_TableCsv_ReturnsDelegate() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Reader_Of_PathDefault_ReturnsDelegate() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Reader_Of_NumberInt_ReturnsDelegate() { throw new System.NotImplementedException("not implemented"); }
    // Stage 2 surface, but the entry exists by Stage 1's end (the catalog is
    // registered up front; Stage 2 wires Read to parse to exact CLR types).
    [Test] public async Task Reader_Of_NumberBigInteger_ReturnsDelegate() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Reader_Of_ImagePng_ReturnsDelegate() { throw new System.NotImplementedException("not implemented"); }
    // TimeSpanIso8601 folds in — duration owns the iso8601 kind.
    [Test] public async Task Reader_Of_DurationIso8601_ReturnsDelegate() { throw new System.NotImplementedException("not implemented"); }
    // crypto.hash's FromWire folds in — hash owns the default-binary kind.
    [Test] public async Task Reader_Of_HashDefault_ReturnsDelegate() { throw new System.NotImplementedException("not implemented"); }
}
