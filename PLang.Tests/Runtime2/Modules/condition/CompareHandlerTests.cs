using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.condition;

namespace PLang.Tests.Runtime2.Modules.condition;

public class CompareHandlerTests
{
    // --- Batch 5: condition.compare Handler ---

    [Test]
    public async Task Run_GreaterThan_ReturnsDataWithTrue()
    {
        // Left=10, Operator=">", Right=5 → Data.Ok(true)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Run_GreaterThan_Fails_ReturnsDataWithFalse()
    {
        // Left=3, Operator=">", Right=5 → Data.Ok(false)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Run_ResultValueIsBool()
    {
        // Data.Value must be specifically bool, not boxed int or string
        Assert.Fail("Not implemented");
    }
}
