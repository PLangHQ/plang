namespace PLang.Tests.App.Serialization;

/// <summary>
/// A Data holding a template source — a step's <c>%original%</c> — written in plang's own format and read back: the
/// item writes itself, and reading those bytes rebuilds what verifies against them. Real signing (no mock), so the
/// read-back is verified against the hash of what was written.
/// </summary>
public class TemplateSourceSignedRoundTripTests
{
    [Test]
    public async Task ASignedTemplateSource_WrittenAndReadBack_Verifies()
    {
        var root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_tsrc_" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            await using var app = new global::app.@this(root);
            app.test.list.Open();
            app.TestIdentity();
            var ctx = app.actor.list.User.Context;
            await ctx.Variable.Set("original", new global::app.data.@this("original", "The quick brown fox",
                app.type.list.Stamp("text/plain", ctx), context: ctx));
            // the Data a step's `Value=%original%` slot holds: a source, the template %original%
            var slot = ctx.Action("archive.pack(Value=%original%)").Property["Value"]!.Data(ctx);
            var plang = app.type.list["wire"].kind["plang"]!;

            using var wire = new System.IO.MemoryStream();
            await (await plang.Encode(wire, slot, ctx)).IsSuccess();
            var back = await plang.Decode(wire.ToArray(), ctx);

            await back.IsSuccess();
            await Assert.That((await back.Value())?.ToString()).IsEqualTo("The quick brown fox");
        }
        finally { System.IO.Directory.Delete(root, true); }
    }
}
