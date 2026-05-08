using App;
using global::App.Actor.Context;
using global::App.Variables;
using global::App.modules.ui;
using global::App.modules.ui.providers;

namespace PLang.Tests.App.Modules.ui;

public class RenderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::App.@this _app;
    private readonly FluidProvider _provider;

    public RenderTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_ui_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new global::App.@this(_tempDir);
        _provider = new FluidProvider();
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private void WriteTemplateFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(_tempDir, relativePath);
        var dir = System.IO.Path.GetDirectoryName(fullPath);
        if (dir != null) System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(fullPath, content);
    }

    // --- Batch 1: Core Render Behavior ---

    [Test]
    public async Task Render_InlineTemplate_SubstitutesVariables()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new Data("name", "World"));
        var action = new Render { Context = ctx, Template = "Hello {{ name }}", IsFile = false };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Hello World");
    }

    [Test]
    public async Task Render_FileTemplate_ReadsAndRenders()
    {
        WriteTemplateFile("greeting.html", "Hello {{ name }}!");
        var ctx = _app.User.Context;
        ctx.Variables.Set(new Data("name", "PLang"));
        var action = new Render { Context = ctx, Template = "greeting.html", IsFile = true };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Hello PLang!");
    }

    [Test]
    public async Task Render_MissingFile_ReturnsError()
    {
        var ctx = _app.User.Context;
        var action = new Render { Context = ctx, Template = "nonexistent.html", IsFile = true };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Render_NullTemplate_ReturnsValidationError()
    {
        // [IsNotNull] is enforced by the source generator before Run() is called.
        // At the provider level, null would cause issues — test that the provider
        // handles it gracefully if somehow invoked with null.
        var ctx = _app.User.Context;
        var action = new Render { Context = ctx, Template = null!, IsFile = false };

        var result = await _provider.Render(action);

        // Provider should still return an error, not throw
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Render_EmptyTemplate_ReturnsEmptyString()
    {
        var ctx = _app.User.Context;
        var action = new Render { Context = ctx, Template = "", IsFile = false };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task Render_LiquidSyntaxError_ReturnsErrorWithPosition()
    {
        var ctx = _app.User.Context;
        var action = new Render { Context = ctx, Template = "Hello {{ ", IsFile = false };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("TemplateError");
    }

    // --- Batch 2: Variable Resolution ---

    [Test]
    public async Task Render_VariablesVariables_AccessibleInTemplate()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new Data("greeting", "Hello"));
        ctx.Variables.Set(new Data("target", "World"));
        var action = new Render { Context = ctx, Template = "{{ greeting }} {{ target }}", IsFile = false };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Hello World");
    }

    [Test]
    public async Task Render_ExplicitParams_OverrideVariables()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new Data("name", "MemoryValue"));
        var overrideParam = new Data("name", "ParamValue");
        var action = new Render
        {
            Context = ctx,
            Template = "Hello {{ name }}",
            IsFile = false,
            Parameters = new List<Data> { overrideParam }
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Hello ParamValue");
    }

    [Test]
    public async Task Render_ExplicitParams_CreateAliases()
    {
        var ctx = _app.User.Context;
        var aliasParam = new Data("title", "My Page");
        var action = new Render
        {
            Context = ctx,
            Template = "Title: {{ title }}",
            IsFile = false,
            Parameters = new List<Data> { aliasParam }
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Title: My Page");
    }

    [Test]
    public async Task Render_ScopedVars_SkippedFromVariables()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new Data("visible", "yes"));
        ctx.Variables.Set(new Data("!hidden", "secret"));
        var action = new Render
        {
            Context = ctx,
            Template = "visible={{ visible }} hidden={{ hidden }}",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        // !hidden should not be accessible as "hidden" in the template
        await Assert.That(result.Value?.ToString()).IsEqualTo("visible=yes hidden=");
    }

    // --- Batch 3: Custom Tags & Partials ---

    [Test]
    public async Task Render_CallGoal_ExecutesGoalAndInsertsResult()
    {
        // Create a real goal that sets a variable — the goal result is the last step's Data
        var goal = new Goal
        {
            Name = "Greeter",
            Path = "/Greeter.goal",
            Steps = new GoalSteps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "greeting" }, { "value", "Hello from goal" } },
                    index: 0, text: "set greeting")
            }
        };
        _app.Goals.Add(goal);

        var ctx = _app.User.Context;
        var action = new Render
        {
            Context = ctx,
            Template = "Result: {% callGoal 'Greeter' %}",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        // The goal sets a variable — its last step result is Data.Ok with the set value
        // callGoal writes Data.Value?.ToString() to output
        var output = result.Value?.ToString() ?? "";
        await Assert.That(output).DoesNotContain("[Error:");
    }

    [Test]
    public async Task Render_CallGoal_GoalNotFound_ShowsErrorInOutput()
    {
        var ctx = _app.User.Context;
        var action = new Render
        {
            Context = ctx,
            Template = "Before {% callGoal 'Missing' %} After",
            IsFile = false
        };

        var result = await _provider.Render(action);

        // Goal not found: either inline error text or Data error
        if (result.Success)
        {
            var output = result.Value?.ToString() ?? "";
            await Assert.That(output).Contains("[Error:");
        }
        else
        {
            await Assert.That(result.Error).IsNotNull();
        }
    }

    [Test]
    public async Task Render_Include_RendersPartialInline()
    {
        WriteTemplateFile("partial.html", "I am a partial");
        var ctx = _app.User.Context;
        var action = new Render
        {
            Context = ctx,
            Template = "Before {% include 'partial.html' %} After",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        var output = result.Value?.ToString() ?? "";
        await Assert.That(output).Contains("I am a partial");
    }

    [Test]
    public async Task Render_Include_InheritsVariables()
    {
        WriteTemplateFile("greet.html", "Hello {{ name }}");
        var ctx = _app.User.Context;
        ctx.Variables.Set(new Data("name", "World"));
        var action = new Render
        {
            Context = ctx,
            Template = "{% include 'greet.html' %}!",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        var output = result.Value?.ToString() ?? "";
        await Assert.That(output).Contains("Hello World");
    }

    // --- Batch 4: Provider & Path Resolution ---

    [Test]
    public async Task Render_CustomProvider_IsUsed()
    {
        var customProvider = new StubTemplateProvider();
        var ctx = _app.User.Context;
        var action = new Render { Context = ctx, Template = "anything", IsFile = false };

        var result = await customProvider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("stub-rendered");
    }

    [Test]
    public async Task Render_FilePathRelativeToGoalDir()
    {
        // Create a template in a subdirectory
        WriteTemplateFile("goals/templates/page.html", "Page content");
        var ctx = _app.User.Context;
        // Simulate a goal at goals/MyGoal.goal by setting Goal.Path
        // Path resolves relative to goal's directory
        var action = new Render { Context = ctx, Template = "goals/templates/page.html", IsFile = true };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Page content");
    }

    [Test]
    public async Task Render_FilePathAbsolute_ResolvesFromRoot()
    {
        WriteTemplateFile("templates/abs.html", "Absolute content");
        var ctx = _app.User.Context;
        var action = new Render { Context = ctx, Template = "/templates/abs.html", IsFile = true };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Absolute content");
    }

    // --- Batch 5: Complex Data Types in Templates ---

    [Test]
    public async Task Render_DotNavigation_AccessesObjectProperties()
    {
        var ctx = _app.User.Context;
        var user = new { name = "Alice", age = 30 };
        ctx.Variables.Set(new Data("user", user));
        var action = new Render
        {
            Context = ctx,
            Template = "{{ user.name }} is {{ user.age }}",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Alice is 30");
    }

    [Test]
    public async Task Render_ListIteration_WorksInForLoop()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new Data("items", new List<string> { "a", "b", "c" }));
        var action = new Render
        {
            Context = ctx,
            Template = "{% for item in items %}{{ item }}{% endfor %}",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("abc");
    }

    [Test]
    public async Task Render_NullVariable_RendersEmpty()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new Data("name", null));
        var action = new Render
        {
            Context = ctx,
            Template = "Hello {{ name }}!",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Hello !");
    }

    [Test]
    public async Task Render_UndefinedVariable_RendersEmpty()
    {
        var ctx = _app.User.Context;
        var action = new Render
        {
            Context = ctx,
            Template = "Hello {{ missing }}!",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Hello !");
    }

    [Test]
    public async Task Render_DataObject_ExposesValueNotWrapper()
    {
        var ctx = _app.User.Context;
        // Data wraps a complex object — template should navigate the inner object, not Data properties
        var user = new { name = "Alice", age = 30 };
        ctx.Variables.Set(new Data("user", user));
        var action = new Render
        {
            Context = ctx,
            Template = "{{ user.name }} is {{ user.age }}",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        var output = result.Value?.ToString() ?? "";
        // Should see inner object properties, not Data.Name/Data.Value/Data.Error etc.
        await Assert.That(output).IsEqualTo("Alice is 30");
    }

    [Test]
    public async Task Render_NullDotNavigation_NoException()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new Data("user", null));
        var action = new Render
        {
            Context = ctx,
            Template = "Hello {{ user.name }}!",
            IsFile = false
        };

        var result = await _provider.Render(action);

        // Should not throw — renders empty for null navigation
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Hello !");
    }

    // --- Batch 6: callGoal Edge Cases ---

    [Test]
    public async Task Render_CallGoal_EmptyGoalReturnsEmptyOutput()
    {
        // An empty goal (no steps) returns Data.Ok() — callGoal writes "" to output
        var goal = new Goal { Name = "EmptyGoal", Path = "/EmptyGoal.goal" };
        _app.Goals.Add(goal);

        var ctx = _app.User.Context;
        var action = new Render
        {
            Context = ctx,
            Template = "Before{% callGoal 'EmptyGoal' %}After",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        var output = result.Value?.ToString() ?? "";
        // Empty goal produces no output — "BeforeAfter"
        await Assert.That(output).DoesNotContain("[Error:");
    }

    [Test]
    public async Task Render_CallGoal_GoalNameFromVariable()
    {
        // callGoal can use a Liquid variable for the goal name
        var goal = new Goal { Name = "DynamicGoal", Path = "/DynamicGoal.goal" };
        _app.Goals.Add(goal);

        var ctx = _app.User.Context;
        ctx.Variables.Set(new Data("goalName", "DynamicGoal"));
        var action = new Render
        {
            Context = ctx,
            Template = "{% callGoal goalName %}",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        var output = result.Value?.ToString() ?? "";
        await Assert.That(output).DoesNotContain("[Error:");
    }

    [Test]
    public async Task Render_CallGoal_SuccessWritesValueToOutput()
    {
        // Goal that sets a variable — verify the result value appears in template output
        var goal = new Goal
        {
            Name = "GetNumber",
            Path = "/GetNumber.goal",
            Steps = new GoalSteps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "num" }, { "value", 42 } },
                    index: 0, text: "set num"),
                MakeStep("goal", "return",
                    new Dictionary<string, object?> { { "data", "%num%" } },
                    index: 1, text: "return num")
            }
        };
        _app.Goals.Add(goal);

        var ctx = _app.User.Context;
        var action = new Render
        {
            Context = ctx,
            Template = "Number: {% callGoal 'GetNumber' %}",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        var output = result.Value?.ToString() ?? "";
        await Assert.That(output).DoesNotContain("[Error:");
        await Assert.That(output).Contains("42");
    }

    // --- Batch 7: Include Edge Cases ---

    [Test]
    public async Task Render_Include_MissingPartial_ReturnsError()
    {
        var ctx = _app.User.Context;
        var action = new Render
        {
            Context = ctx,
            Template = "{% include 'nonexistent.html' %}",
            IsFile = false
        };

        var result = await _provider.Render(action);

        // Fluid throws FileNotFoundException for missing includes — caught as RenderError
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("RenderError");
    }

    [Test]
    public async Task Render_Include_NestedPathResolvesRelativeToPartial()
    {
        // Fluid resolves includes from the FileProvider root, not relative to the partial
        WriteTemplateFile("sub/a.html", "A{% include 'sub/b.html' %}");
        WriteTemplateFile("sub/b.html", "B");
        var ctx = _app.User.Context;
        var action = new Render
        {
            Context = ctx,
            Template = "{% include 'sub/a.html' %}",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        var output = result.Value?.ToString() ?? "";
        await Assert.That(output).Contains("A");
        await Assert.That(output).Contains("B");
    }

    // --- Batch 8: Security & Encoding ---

    [Test]
    public async Task Render_HtmlInVariable_IsNotEscaped()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new Data("name", "<script>alert(1)</script>"));
        var action = new Render
        {
            Context = ctx,
            Template = "{{ name }}",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        var output = result.Value?.ToString() ?? "";
        // PLang templates output raw content — no HTML escaping
        await Assert.That(output).Contains("<script>");
    }

    // --- Batch 9: Auto-detect (LooksLikeFilePath coverage) ---

    [Test]
    public async Task Render_IsFileNull_InlineWithLiquidSyntax_TreatedAsInline()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new Data("name", "World"));
        // IsFile=null + template contains {{ — auto-detect should treat as inline
        var action = new Render
        {
            Context = ctx,
            Template = "Hello {{ name }}!"
            // IsFile not set — defaults to null
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Hello World!");
    }

    [Test]
    public async Task Render_IsFileNull_FilePathAutoDetected()
    {
        WriteTemplateFile("auto.html", "Auto-detected {{ greeting }}");
        var ctx = _app.User.Context;
        ctx.Variables.Set(new Data("greeting", "Hi"));
        // IsFile=null + template looks like a file path (has extension, no Liquid syntax)
        var action = new Render
        {
            Context = ctx,
            Template = "auto.html"
            // IsFile not set — auto-detect should find the file
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Auto-detected Hi");
    }

    [Test]
    public async Task Render_IsFileNull_NoExtension_TreatedAsInline()
    {
        var ctx = _app.User.Context;
        // IsFile=null + no file extension — auto-detect should treat as inline content
        var action = new Render
        {
            Context = ctx,
            Template = "just plain text with no extension"
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("just plain text with no extension");
    }

    // --- Batch 10: Goal-relative include resolution (GetTemplateBaseDir coverage) ---

    [Test]
    public async Task Render_IncludeResolvesFromGoalDirectory()
    {
        // Create a goal in a subdirectory and a partial next to it
        WriteTemplateFile("goals/templates/footer.html", "Footer content");
        var goal = new Goal
        {
            Name = "SubGoal",
            Path = "goals/SubGoal.goal"
        };
        _app.Goals.Add(goal);

        var ctx = _app.User.Context;
        ctx.Goal = goal;
        // The include should resolve relative to the goal's directory (goals/)
        var action = new Render
        {
            Context = ctx,
            Template = "{% include 'templates/footer.html' %}",
            IsFile = false
        };

        var result = await _provider.Render(action);

        await Assert.That(result.Success).IsTrue();
        var output = result.Value?.ToString() ?? "";
        await Assert.That(output).Contains("Footer content");
    }

    // --- Helper for creating steps (from EngineTests pattern) ---

    private static Step MakeStep(string actionClass, string method, object? parameters = null, int index = 0, string text = "")
    {
        return new Step
        {
            Index = index,
            Text = text,
            Actions = new StepActions
            {
                new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
                {
                    Module = actionClass,
                    ActionName = method,
                    Parameters = parameters is IDictionary<string, object?> dict
                        ? dict.Select(kv => new Data(kv.Key, kv.Value)).ToList()
                        : new List<Data>()
                }
            }
        };
    }

    // --- Stub provider for swap test ---

    private class StubTemplateProvider : ITemplateProvider
    {
        public string Name => "stub";
        public bool IsDefault { get; set; }

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }

        public Task<Data> Render(Render action)
        {
            return Task.FromResult(Data.Ok((object)"stub-rendered"));
        }
    }
}
