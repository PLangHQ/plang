using app.module.math;
using PLangEngine = global::app.@this;
using ExampleSpec = global::app.type.spec.Example;

namespace PLang.Tests.App.actions.math;

/// <summary>
/// Pins the rendered output of each math action's <c>ExamplesForLlm()</c>.
///
/// The integration signal (Loop.test.goal producing 3 instead of "0+1+1+1") proves
/// the LLM picks up the examples — but it's mediated by a live LLM call. A renderer
/// or example-spec regression that broke the formal-string output would only surface
/// when someone happens to rebuild a goal that matches the RHS-arithmetic pattern.
/// These tests assert the rendered chain directly so a regression fails C# first.
/// </summary>
public class MathExamplesForLlmTests
{
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/test");
        _app.Build = new global::app.module.build.@this(_app.System.Context);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try { await _app.DisposeAsync(); } catch { /* best effort */ }
    }

    private string Render(ExampleSpec spec) => _app.Module.Schema.Render(spec);

    // --- Add ---

    [Test]
    public async Task Add_NaturalForm_RendersAddThenSet()
    {
        var specs = Add.ExamplesForLlm();
        await Assert.That(specs.Length).IsEqualTo(2);

        var rendered = Render(specs[0]);
        await Assert.That(specs[0].UserIntent).IsEqualTo("add 5 and 3, write to %sum%");
        await Assert.That(rendered).Contains("math.add");
        await Assert.That(rendered).Contains("variable.set");
        await Assert.That(rendered).Contains("A([object] 5)");
        await Assert.That(rendered).Contains("B([object] 3)");
        await Assert.That(rendered).Contains("Name([string] %sum%)");
        await Assert.That(rendered).Contains("Value([object] %!data%)");
    }

    [Test]
    public async Task Add_RhsForm_RendersAddThenSet_WithVarOperand()
    {
        var specs = Add.ExamplesForLlm();
        var rendered = Render(specs[1]);

        await Assert.That(specs[1].UserIntent).IsEqualTo("set %count% = %count% + 1");
        await Assert.That(rendered).Contains("math.add");
        await Assert.That(rendered).Contains("variable.set");
        await Assert.That(rendered).Contains("%count%");
        await Assert.That(rendered).Contains("Value([object] %!data%)");
    }

    // --- Subtract ---

    [Test]
    public async Task Subtract_BothForms_RenderChain()
    {
        var specs = Subtract.ExamplesForLlm();
        await Assert.That(specs.Length).IsEqualTo(2);

        var natural = Render(specs[0]);
        await Assert.That(natural).Contains("math.subtract");
        await Assert.That(natural).Contains("variable.set");
        await Assert.That(natural).Contains("A([object] 10)");
        await Assert.That(natural).Contains("B([object] 3)");

        var rhs = Render(specs[1]);
        await Assert.That(rhs).Contains("math.subtract");
        await Assert.That(rhs).Contains("%total%");
        await Assert.That(rhs).Contains("%discount%");
    }

    // --- Multiply ---

    [Test]
    public async Task Multiply_BothForms_RenderChain()
    {
        var specs = Multiply.ExamplesForLlm();
        await Assert.That(specs.Length).IsEqualTo(2);

        var natural = Render(specs[0]);
        await Assert.That(natural).Contains("math.multiply");
        await Assert.That(natural).Contains("variable.set");
        await Assert.That(natural).Contains("A([object] 6)");
        await Assert.That(natural).Contains("B([object] 7)");

        var rhs = Render(specs[1]);
        await Assert.That(rhs).Contains("math.multiply");
        await Assert.That(rhs).Contains("%width%");
        await Assert.That(rhs).Contains("%height%");
    }

    // --- Divide ---

    [Test]
    public async Task Divide_BothForms_RenderChain()
    {
        var specs = Divide.ExamplesForLlm();
        await Assert.That(specs.Length).IsEqualTo(2);

        var natural = Render(specs[0]);
        await Assert.That(natural).Contains("math.divide");
        await Assert.That(natural).Contains("variable.set");
        await Assert.That(natural).Contains("A([object] 10)");
        await Assert.That(natural).Contains("B([object] 4)");

        var rhs = Render(specs[1]);
        await Assert.That(rhs).Contains("math.divide");
        await Assert.That(rhs).Contains("%total%");
        await Assert.That(rhs).Contains("%count%");
    }

    // --- Power ---

    [Test]
    public async Task Power_BothForms_RenderChain()
    {
        var specs = Power.ExamplesForLlm();
        await Assert.That(specs.Length).IsEqualTo(2);

        var natural = Render(specs[0]);
        await Assert.That(natural).Contains("math.power");
        await Assert.That(natural).Contains("variable.set");
        await Assert.That(natural).Contains("Base([object] 2)");
        await Assert.That(natural).Contains("Exponent([object] 3)");

        var rhs = Render(specs[1]);
        await Assert.That(rhs).Contains("math.power");
        await Assert.That(rhs).Contains("%x%");
    }

    // --- Cross-action: chaining structure is consistent ---

    [Test]
    public async Task AllArithmeticActions_HaveTwoExamples_NaturalAndRhs()
    {
        var sets = new (string name, ExampleSpec[] specs)[]
        {
            ("add",      Add.ExamplesForLlm()),
            ("subtract", Subtract.ExamplesForLlm()),
            ("multiply", Multiply.ExamplesForLlm()),
            ("divide",   Divide.ExamplesForLlm()),
            ("power",    Power.ExamplesForLlm()),
        };

        foreach (var (name, specs) in sets)
        {
            await Assert.That(specs.Length).IsEqualTo(2);
            await Assert.That(specs[1].UserIntent).Contains("set ");
            // Each example is a 2-action chain: math.<op> followed by variable.set
            foreach (var spec in specs)
            {
                await Assert.That(spec.Chain.Length).IsEqualTo(2);
                await Assert.That(spec.Chain[0].Module).IsEqualTo("math");
                await Assert.That(spec.Chain[0].Name).IsEqualTo(name);
                await Assert.That(spec.Chain[1].Module).IsEqualTo("variable");
                await Assert.That(spec.Chain[1].Name).IsEqualTo("set");
            }
        }
    }
}
