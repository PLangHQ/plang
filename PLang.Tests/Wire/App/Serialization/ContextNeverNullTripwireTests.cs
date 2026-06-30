namespace PLang.Tests.App.Serialization;

/// <summary>
/// Stage 6 invariant proof: a Store-view Data read with no context is impossible by
/// construction (every per-actor serializer carries one). The tripwire in Wire.ReadCore
/// makes a regression LOUD instead of silently falling back to the retired context-less
/// narrow that dropped nested typed entries.
/// </summary>
public class ContextNeverNullTripwireTests
{
    [Test]
    public async Task StoreRead_WithNoContext_Throws()
    {
        // A context-less Wire (the default ctor) in Store view — the exact regression the
        // tripwire guards. Reading any Data wire object must throw, not proceed.
        var wire = new global::app.data.Wire(global::app.View.Store);
        var bytes = System.Text.Encoding.UTF8.GetBytes("{\"@schema\":\"data\",\"name\":\"x\",\"type\":{\"name\":\"text\"},\"value\":\"hi\"}");
        var options = global::app.channel.serializer.json.Options.Read(null);

        var threw = false;
        try { wire.ReadBuffered(bytes, options); }
        catch (System.Text.Json.JsonException ex) when (ex.Message.Contains("context-never-null")) { threw = true; }
        await Assert.That(threw).IsTrue();
    }
}
