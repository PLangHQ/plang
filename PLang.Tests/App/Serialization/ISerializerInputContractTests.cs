namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 1
// ISerializer input tightened to Data. Coverage matrix rows 1.1, 1.2, 1.7, 1.8, 1.11.
// Architect refs: .bot/data-serialize-cleanup/architect/stage-1-iserializer-data.md,
//                 .bot/data-serialize-cleanup/architect/plan/test-coverage.md.

public class ISerializerInputContractTests
{
    // 1.1 — ISerializer.SerializeAsync(Stream, Data, ct) accepts Data and returns Task<Data>.
    [Test]
    public async Task SerializeAsync_AcceptsDataArgument_ReturnsTaskData()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    // 1.2 — Old polymorphic SerializeAsync(Stream, object, …) overload is gone.
    //       Expressed as a reflection check; the compile-time guard is the real one
    //       (calling sites cease to compile if the overload returns).
    [Test]
    public async Task SerializeAsync_PolymorphicObjectOverload_NotPresentOnInterface()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    // 1.7 — SerializeOptions.Type carries the MIME string (renamed from ContentType).
    [Test]
    public async Task SerializeOptions_Type_CarriesMimeString()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    // 1.8 — SerializeOptions.Data is typed as Data (not object?).
    [Test]
    public async Task SerializeOptions_Data_IsTypedAsData()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    // 1.11 — Stream channel's renamed Write hook passes the full Data into the registered
    //        serializer (not data.Value as before). Closes the "strip-then-rebuild" bug.
    [Test]
    public async Task StreamChannel_Write_HandsFullDataToSerializer_NotValueOnly()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    // SerializeOptions.Type-old: the previous ContentType property is gone.
    [Test]
    public async Task SerializeOptions_ContentType_PropertyRemoved()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    // DeserializeOptions / ResolveOptions: same Type rename — both compile in usage.
    [Test]
    public async Task DeserializeOptions_Type_CarriesMimeString_NoContentTypeProperty()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ResolveOptions_Type_CarriesMimeString_NoContentTypeProperty()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
