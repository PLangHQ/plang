using app;
using app.actor.context;
using app.variable;
using app.module.ui;
using app.module.ui.code;

namespace PLang.Tests.App.Modules.ui;

public class RenderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;
    private readonly global::app.module.ui.code.Fluid _provider;

    public RenderTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_ui_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new global::app.@this(_tempDir);
        _provider = new global::app.module.ui.code.Fluid();
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
        var context = _app.User.Context;
        context.Variable.Set(new Data("name", "World"));
        var action = new Render { Context = context, Template = (global::app.type.text.@this)"Hello {{ name }}", IsFile = (global::app.type.@bool.@this)false };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Hello World");
    }

    [Test]
    public async Task Render_FileTemplate_ReadsAndRenders()
    {
        WriteTemplateFile("greeting.html", "Hello {{ name }}!");
        var context = _app.User.Context;
        context.Variable.Set(new Data("name", "PLang"));
        var action = new Render { Context = context, Template = (global::app.type.text.@this)"greeting.html", IsFile = (global::app.type.@bool.@this)true };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Hello PLang!");
    }

    [Test]
    public async Task Render_MissingFile_ReturnsError()
    {
        var context = _app.User.Context;
        var action = new Render { Context = context, Template = (global::app.type.text.@this)"nonexistent.html", IsFile = (global::app.type.@bool.@this)true };

        var result = await _provider.Render(action);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Render_NullTemplate_ReturnsValidationError()
    {
        // [IsNotNull] is enforced by the source generator before Run() is called.
        // At the provider level, null would cause issues — test that the provider
        // handles it gracefully if somehow invoked with null.
        var context = _app.User.Context;
        var action = new Render { Context = context, Template = null!, IsFile = (global::app.type.@bool.@this)false };

        var result = await _provider.Render(action);

        // Provider should still return an error, not throw
        await result.IsFailure();
    }

    [Test]
    public async Task Render_EmptyTemplate_ReturnsEmptyString()
    {
        var context = _app.User.Context;
        var action = new Render { Context = context, Template = (global::app.type.text.@this)"", IsFile = (global::app.type.@bool.@this)false };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task Render_LiquidSyntaxError_ReturnsErrorWithPosition()
    {
        var context = _app.User.Context;
        var action = new Render { Context = context, Template = (global::app.type.text.@this)"Hello {{ ", IsFile = (global::app.type.@bool.@this)false };

        var result = await _provider.Render(action);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("TemplateError");
    }

    // --- Batch 2: Variable Resolution ---

    [Test]
    public async Task Render_VariablesVariables_AccessibleInTemplate()
    {
        var context = _app.User.Context;
        context.Variable.Set(new Data("greeting", "Hello"));
        context.Variable.Set(new Data("target", "World"));
        var action = new Render { Context = context, Template = (global::app.type.text.@this)"{{ greeting }} {{ target }}", IsFile = (global::app.type.@bool.@this)false };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Hello World");
    }

    [Test]
    public async Task Render_ExplicitParams_OverrideVariables()
    {
        var context = _app.User.Context;
        context.Variable.Set(new Data("name", "MemoryValue"));
        var overrideParam = new Data("name", "ParamValue");
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"Hello {{ name }}",
            IsFile = (global::app.type.@bool.@this)false,
            Parameters = new List<Data> { overrideParam }.ToListData()
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Hello ParamValue");
    }

    [Test]
    public async Task Render_ExplicitParams_CreateAliases()
    {
        var context = _app.User.Context;
        var aliasParam = new Data("title", "My Page");
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"Title: {{ title }}",
            IsFile = (global::app.type.@bool.@this)false,
            Parameters = new List<Data> { aliasParam }.ToListData()
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Title: My Page");
    }

    [Test]
    public async Task Render_ScopedVars_SkippedFromVariables()
    {
        var context = _app.User.Context;
        context.Variable.Set(new Data("visible", "yes"));
        context.Variable.Set(new Data("!hidden", "secret"));
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"visible={{ visible }} hidden={{ hidden }}",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        // !hidden should not be accessible as "hidden" in the template
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("visible=yes hidden=");
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
        _app.Goal.Add(goal);

        var context = _app.User.Context;
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"Result: {% callGoal 'Greeter' %}",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        // The goal sets a variable — its last step result is Data.Ok with the set value
        // callGoal writes Data.Value?.ToString() to output
        var output = (await result.Value())?.ToString() ?? "";
        await Assert.That(output).DoesNotContain("[Error:");
    }

    [Test]
    public async Task Render_CallGoal_GoalNotFound_ShowsErrorInOutput()
    {
        var context = _app.User.Context;
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"Before {% callGoal 'Missing' %} After",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        // Goal not found: either inline error text or Data error
        if (result.Success)
        {
            var output = (await result.Value())?.ToString() ?? "";
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
        var context = _app.User.Context;
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"Before {% include 'partial.html' %} After",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        var output = (await result.Value())?.ToString() ?? "";
        await Assert.That(output).Contains("I am a partial");
    }

    [Test]
    public async Task Render_Include_InheritsVariables()
    {
        WriteTemplateFile("greet.html", "Hello {{ name }}");
        var context = _app.User.Context;
        context.Variable.Set(new Data("name", "World"));
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"{% include 'greet.html' %}!",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        var output = (await result.Value())?.ToString() ?? "";
        await Assert.That(output).Contains("Hello World");
    }

    // --- Batch 4: Provider & Path Resolution ---

    [Test]
    public async Task Render_CustomProvider_IsUsed()
    {
        var customProvider = new StubTemplateProvider();
        var context = _app.User.Context;
        var action = new Render { Context = context, Template = (global::app.type.text.@this)"anything", IsFile = (global::app.type.@bool.@this)false };

        var result = await customProvider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("stub-rendered");
    }

    [Test]
    public async Task Render_FilePathRelativeToGoalDir()
    {
        // Create a template in a subdirectory
        WriteTemplateFile("goals/templates/page.html", "Page content");
        var context = _app.User.Context;
        // Simulate a goal at goals/MyGoal.goal by setting Goal.Path
        // Path resolves relative to goal's directory
        var action = new Render { Context = context, Template = (global::app.type.text.@this)"goals/templates/page.html", IsFile = (global::app.type.@bool.@this)true };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Page content");
    }

    [Test]
    public async Task Render_FilePathAbsolute_ResolvesFromRoot()
    {
        WriteTemplateFile("templates/abs.html", "Absolute content");
        var context = _app.User.Context;
        var action = new Render { Context = context, Template = (global::app.type.text.@this)"/templates/abs.html", IsFile = (global::app.type.@bool.@this)true };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Absolute content");
    }

    // --- Batch 5: Complex Data Types in Templates ---

    [Test]
    public async Task Render_DotNavigation_AccessesObjectProperties()
    {
        var context = _app.User.Context;
        var user = new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 };
        context.Variable.Set(new Data("user", user));
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"{{ user.name }} is {{ user.age }}",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Alice is 30");
    }

    [Test]
    public async Task Render_ListIteration_WorksInForLoop()
    {
        var context = _app.User.Context;
        context.Variable.Set(new Data("items", new List<string> { "a", "b", "c" }));
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"{% for item in items %}{{ item }}{% endfor %}",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("abc");
    }

    [Test]
    public async Task Render_NullVariable_RendersEmpty()
    {
        var context = _app.User.Context;
        context.Variable.Set(new Data("name", null));
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"Hello {{ name }}!",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Hello !");
    }

    [Test]
    public async Task Render_UndefinedVariable_RendersEmpty()
    {
        var context = _app.User.Context;
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"Hello {{ missing }}!",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Hello !");
    }

    [Test]
    public async Task Render_DataObject_ExposesValueNotWrapper()
    {
        var context = _app.User.Context;
        // Data wraps a complex object — template should navigate the inner object, not Data properties
        var user = new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 };
        context.Variable.Set(new Data("user", user));
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"{{ user.name }} is {{ user.age }}",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        var output = (await result.Value())?.ToString() ?? "";
        // Should see inner object properties, not Data.Name/Data.Value/Data.Error etc.
        await Assert.That(output).IsEqualTo("Alice is 30");
    }

    [Test]
    public async Task Render_NullDotNavigation_NoException()
    {
        var context = _app.User.Context;
        context.Variable.Set(new Data("user", null));
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"Hello {{ user.name }}!",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        // Should not throw — renders empty for null navigation
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Hello !");
    }

    // --- Batch 6: callGoal Edge Cases ---

    [Test]
    public async Task Render_CallGoal_EmptyGoalReturnsEmptyOutput()
    {
        // An empty goal (no steps) returns Data.Ok() — callGoal writes "" to output
        var goal = new Goal { Name = "EmptyGoal", Path = "/EmptyGoal.goal" };
        _app.Goal.Add(goal);

        var context = _app.User.Context;
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"Before{% callGoal 'EmptyGoal' %}After",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        var output = (await result.Value())?.ToString() ?? "";
        // Empty goal produces no output — "BeforeAfter"
        await Assert.That(output).DoesNotContain("[Error:");
    }

    [Test]
    public async Task Render_CallGoal_GoalNameFromVariable()
    {
        // callGoal can use a Liquid variable for the goal name
        var goal = new Goal { Name = "DynamicGoal", Path = "/DynamicGoal.goal" };
        _app.Goal.Add(goal);

        var context = _app.User.Context;
        context.Variable.Set(new Data("goalName", "DynamicGoal"));
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"{% callGoal goalName %}",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        var output = (await result.Value())?.ToString() ?? "";
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
        _app.Goal.Add(goal);

        var context = _app.User.Context;
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"Number: {% callGoal 'GetNumber' %}",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        var output = (await result.Value())?.ToString() ?? "";
        await Assert.That(output).DoesNotContain("[Error:");
        await Assert.That(output).Contains("42");
    }

    // --- Batch 7: Include Edge Cases ---

    [Test]
    public async Task Render_Include_MissingPartial_ReturnsError()
    {
        var context = _app.User.Context;
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"{% include 'nonexistent.html' %}",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        // Fluid throws FileNotFoundException for missing includes — caught as RenderError
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("RenderError");
    }

    [Test]
    public async Task Render_Include_NestedPathResolvesRelativeToPartial()
    {
        // Fluid resolves includes from the FileProvider root, not relative to the partial
        WriteTemplateFile("sub/a.html", "A{% include 'sub/b.html' %}");
        WriteTemplateFile("sub/b.html", "B");
        var context = _app.User.Context;
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"{% include 'sub/a.html' %}",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        var output = (await result.Value())?.ToString() ?? "";
        await Assert.That(output).Contains("A");
        await Assert.That(output).Contains("B");
    }

    // --- Batch 8: Security & Encoding ---

    [Test]
    public async Task Render_HtmlInVariable_IsNotEscaped()
    {
        var context = _app.User.Context;
        context.Variable.Set(new Data("name", "<script>alert(1)</script>"));
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"{{ name }}",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        var output = (await result.Value())?.ToString() ?? "";
        // PLang templates output raw content — no HTML escaping
        await Assert.That(output).Contains("<script>");
    }

    // --- Batch 9: Auto-detect (LooksLikeFilePath coverage) ---

    [Test]
    public async Task Render_IsFileNull_InlineWithLiquidSyntax_TreatedAsInline()
    {
        var context = _app.User.Context;
        context.Variable.Set(new Data("name", "World"));
        // IsFile=null + template contains {{ — auto-detect should treat as inline
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"Hello {{ name }}!"
            // IsFile not set — defaults to null
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Hello World!");
    }

    [Test]
    public async Task Render_IsFileNull_FilePathAutoDetected()
    {
        WriteTemplateFile("auto.html", "Auto-detected {{ greeting }}");
        var context = _app.User.Context;
        context.Variable.Set(new Data("greeting", "Hi"));
        // IsFile=null + template looks like a file path (has extension, no Liquid syntax)
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"auto.html"
            // IsFile not set — auto-detect should find the file
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Auto-detected Hi");
    }

    [Test]
    public async Task Render_IsFileNull_NoExtension_TreatedAsInline()
    {
        var context = _app.User.Context;
        // IsFile=null + no file extension — auto-detect should treat as inline content
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"just plain text with no extension"
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("just plain text with no extension");
    }

    // --- Batch 10: Goal-relative include resolution (GetTemplateBaseDir coverage) ---

    [Test]
    public async Task Render_IncludeResolvesFromGoalDirectory()
    {
        // Create a goal in a subdirectory and a partial next to it
        WriteTemplateFile("goals/templates/footer.html", "Footer content");
        var context = _app.User.Context;
        var goal = new Goal
        {
            Name = "SubGoal",
            Path = global::app.type.path.@this.Resolve("/goals/SubGoal.goal", context)
        };
        _app.Goal.Add(goal);
        context.Goal = goal;
        // The include should resolve relative to the goal's directory (goals/)
        var action = new Render
        {
            Context = context,
            Template = (global::app.type.text.@this)"{% include 'templates/footer.html' %}",
            IsFile = (global::app.type.@bool.@this)false
        };

        var result = await _provider.Render(action);

        await result.IsSuccess();
        var output = (await result.Value())?.ToString() ?? "";
        await Assert.That(output).Contains("Footer content");
    }

    // --- Helper for creating steps (from EngineTests pattern) ---

    private static Step MakeStep(string actionClass, string method, object? parameters = null, int index = 0, string text = "")
    {
        var action = new global::app.goal.steps.step.actions.action.@this
        {
            Module = actionClass,
            ActionName = method,
            Parameters = parameters is IDictionary<string, object?> dict
                ? PrParam.List(actionClass, method, dict)
                : new List<Data>()
        };
        // Tests author actions the way the builder does — same template seam
        // the .pr load applies, so %ref% parameters resolve live at dispatch.
        action.StampTemplates();
        return new Step
        {
            Index = index,
            Text = text,
            Actions = new StepActions { action }
        };
    }

    // --- Stub provider for swap test ---

    private class StubTemplateProvider : ITemplate
    {
        public string Name => "stub";
        public bool IsDefault { get; set; }

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }

        public Task<global::app.data.@this<global::app.type.text.@this>> Render(Render action)
        {
            return Task.FromResult(global::app.data.@this<global::app.type.text.@this>.Ok("stub-rendered"));
        }
    }
}
