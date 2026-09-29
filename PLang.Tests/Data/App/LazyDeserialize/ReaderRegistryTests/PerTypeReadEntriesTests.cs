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
    // json is NOT a reader-registry entry — object is not a plang type, so the (object,json)
    // reader is gone; the json KIND owns the decode (Kind["json"].Load → clr). Documents the
    // removal so nothing re-registers it.
    [Test] public async Task Of_ObjectJson_IsGone_JsonOwnedByKind()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Of("object", "json")).IsNull();
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
    // number reads through its typed reader (each storage kind reads its own token).
    [Test] public async Task Reader_Of_NumberInt_ReturnsTypedReader()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Typed("number", "int")).IsNotNull();
    }
    [Test] public async Task Reader_Of_NumberBigInteger_ReturnsTypedReader()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Typed("number", "biginteger")).IsNotNull();
    }
    [Test] public async Task Reader_Of_ImagePng_ReturnsDelegate()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Of("image", "png")).IsNotNull();
    }
    // duration owns the iso8601 kind in the reader registry (its Read parses
    // ISO-8601 + .NET forms).
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
