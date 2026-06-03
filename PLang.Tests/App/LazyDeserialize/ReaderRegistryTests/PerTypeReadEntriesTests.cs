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
    // Independent #3 — the registry-level "the (text, json) entry exists"
    // probe. Decision 1 sets text/json as the dispatch shape for json bodies.
    [Test] public async Task Reader_Of_TextJson_ReturnsDelegate() { throw new System.NotImplementedException("not implemented"); }
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
