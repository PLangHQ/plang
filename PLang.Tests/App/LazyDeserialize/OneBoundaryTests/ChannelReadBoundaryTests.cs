using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// Stage 4 — channel is the foundational I/O layer; there is one read verb,
// `channel.read`. Every read enters here. It stamps `type`/`kind` from the
// channel's `Mime` and produces lazy Data.
public class ChannelReadBoundaryTests
{
    [Test] public async Task ChannelRead_StampsTypeKind_FromMime() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task ChannelRead_ProducesLazyData_RawSetValueNull() { throw new System.NotImplementedException("not implemented"); }

    // Independent #14 — channel.this.cs:38 defaults Mime to "text/plain".
    // The default stamp must be `{text, null}` — careless implementations
    // could stamp `{object, null}` or throw on an unset Mime.
    [Test] public async Task ChannelRead_DefaultTextPlainMime_StampsTextNullKind() { throw new System.NotImplementedException("not implemented"); }

    // Independent #15 — Decision 3 says raw is bytes only where genuinely
    // bytes. An octet-stream Mime stamps `{bytes, null}` *and* `_raw` is
    // `byte[]`, not `string`.
    [Test] public async Task ChannelRead_OctetStreamMime_StampsBytesAndRawIsByteArray() { throw new System.NotImplementedException("not implemented"); }

    // Decision (Part 1) — structured-text MIMEs map to `{text, kind}`,
    // not `{object, kind}`. So `application/json` body lands as
    // `{text, json}` — the json string is the value until navigated.
    [Test] public async Task ChannelRead_ApplicationJsonBody_StampsTextJson_NotObjectJson() { throw new System.NotImplementedException("not implemented"); }

    // `application/plang` is the self-describing container; channel.read
    // hands the body to the plang serializer, which reads the Data
    // container and defers the value slot too.
    [Test] public async Task ChannelRead_ApplicationPlangBody_DelegatesToPlangSerializer_LazyContainer() { throw new System.NotImplementedException("not implemented"); }

    // app/channel/stream/this.cs:69 today returns bare text and ignores
    // the channel's Mime. After Stage 4 the stream channel produces
    // Mime-stamped lazy Data.
    [Test] public async Task StreamChannel_NoLongerReturnsBareText() { throw new System.NotImplementedException("not implemented"); }
}
