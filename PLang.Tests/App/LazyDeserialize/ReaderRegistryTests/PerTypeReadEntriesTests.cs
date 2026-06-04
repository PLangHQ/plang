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
    [Test] public async Task Reader_Of_ObjectJson_ReturnsDelegate()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Of("object", "json")).IsNotNull();
    }

    // The new `table` type's primary kind. `(table, csv)` lands in-branch;
    // `(table, xlsx)` is a follow-on (binary, needs a library) — until
    // then, a .xlsx stamps `{table, xlsx}` and rides as raw bytes.
    [Test] public async Task Reader_Of_TableCsv_ReturnsDelegate()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Of("table", "csv")).IsNotNull();
    }
    [Test] public async Task Reader_Of_PathDefault_ReturnsDelegate()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Of("path", "json")).IsNotNull();
        await Assert.That(r.Of("path", global::app.type.reader.@this.AnyKind)).IsNotNull();
    }
    [Test] public async Task Reader_Of_NumberInt_ReturnsDelegate()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Of("number", "int")).IsNotNull();
    }
    // Stage 2 surface, but the entry exists by Stage 1's end. number ships a
    // Default (wildcard) Read, so (number, biginteger) resolves via the "*"
    // fallback today; Stage 2 wires the exact parse to the CLR type.
    [Test] public async Task Reader_Of_NumberBigInteger_ReturnsDelegate()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Of("number", "biginteger")).IsNotNull();
    }
    [Test] public async Task Reader_Of_ImagePng_ReturnsDelegate()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Of("image", "png")).IsNotNull();
    }
    // duration owns the iso8601 kind in the reader registry (its Read parses
    // ISO-8601 + .NET forms). The format-layer TimeSpanIso8601 STJ converter
    // stays separate (architect call) — see ConverterDeletionsTests.
    [Test] public async Task Reader_Of_DurationIso8601_ReturnsDelegate()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Of("duration", "iso8601")).IsNotNull();
    }
    // crypto.hash's FromWire re-houses as hash's Read (default kind).
    [Test] public async Task Reader_Of_HashDefault_ReturnsDelegate()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Of("hash", global::app.type.reader.@this.AnyKind)).IsNotNull();
    }
}
