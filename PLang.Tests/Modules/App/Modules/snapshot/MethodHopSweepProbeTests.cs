namespace PLang.Tests.App.Modules.snapshot;

// Sweep (snapshot 339): every public [LlmBuilder] METHOD, the only thing a variable's method hop can call
// (type/item/variable/code/Method.cs:65-69). Prints declaring type, signature, return type; asserts nothing.
public class MethodHopSweepProbeTests
{
    [Test]
    public async Task ListEveryMethodAVariableCanCall()
    {
        var found = new List<string>();
        foreach (var type in typeof(global::app.@this).Assembly.GetTypes())
        {
            System.Reflection.MethodInfo[] methods;
            try { methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly); }
            catch { continue; }
            foreach (var method in methods)
                if (System.Attribute.IsDefined(method, typeof(global::app.LlmBuilderAttribute)))
                    found.Add($"{type.FullName} . {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))}) : {method.ReturnType.Name}");
        }
        await TestContext.Current!.OutputWriter.WriteLineAsync("METHODS\n" + string.Join("\n", found.OrderBy(x => x)));
        await Assert.That(found.Count).IsGreaterThanOrEqualTo(0);
    }
}
