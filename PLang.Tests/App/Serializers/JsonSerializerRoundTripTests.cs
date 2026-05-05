namespace PLang.Tests.App.Serializers;

public class JsonSerializerRoundTripTests
{
    [Test]
    public async Task JsonSerializer_Write_EmitsValueOnly_NeverReadsSignature()
    {
        // text/html and application/json wire shape is data.Value only; data.Signature
        // backing field stays null after Write.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task JsonSerializer_Read_ProducesData_WithoutPopulatingSignature()
    {
        // Reading a JSON wire payload reconstructs Data with Value set; Signature stays null.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task JsonSerializer_HandlesTextHtml_AndApplicationJson_MimeTypes()
    {
        // The serializer registers for both mimetypes and produces the same wire shape.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
