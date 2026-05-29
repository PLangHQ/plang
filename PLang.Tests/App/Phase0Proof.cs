using app;
using app.error;
using app.variables;
using app.Utils;
using R2 = global::app.data;

namespace PLang.Tests.App;

/// <summary>
/// Phase 0 proof tests — each test demonstrates a specific phase's behavior
/// with clear input → output mapping for black-box validation.
/// </summary>
public class Phase0Proof
{
    // ================================================================
    // Phase 0.1 — Data.FromError() (renamed from Data.Fail())
    // ================================================================

    [Test]
    public async Task Phase01_DataFromError_CreatesErrorResult()
    {
        // INPUT: create a Data result from an error
        var error = new Error("File not found", "NotFound", 404);
        var result = Data.FromError(error);

        // OUTPUT: Data has error, is not successful
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Message).IsEqualTo("File not found");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Phase01_DataOk_StillWorks()
    {
        // INPUT: create a successful Data result
        var result = Data.Ok("hello world");

        // OUTPUT: Data has value, is successful
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Error).IsNull();
        await Assert.That(result.Value).IsEqualTo("hello world");
    }

    [Test]
    public async Task Phase01_GenericDataFromError()
    {
        // INPUT: generic global::app.data.@this<T>.FromError
        var result = global::app.data.@this<string>.FromError(new Error("oops"));

        // OUTPUT: typed error result
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).IsEqualTo("oops");
    }

    // ================================================================
    // Phase 0.2 — Error Categories
    // ================================================================

    [Test]
    public async Task Phase02_ErrorCategory_400_IsApplication()
    {
        // INPUT: Error with status 400
        var error = new Error("Invalid email", "Validation", 400);

        // OUTPUT: Category = Application
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Application);
    }

    [Test]
    public async Task Phase02_ErrorCategory_500_IsRuntime()
    {
        // INPUT: Error with status 500
        var error = new Error("Null reference", "NullRef", 500);

        // OUTPUT: Category = Runtime
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Runtime);
    }

    [Test]
    public async Task Phase02_ValidationError_AlwaysApplication_Even500()
    {
        // INPUT: ValidationError with status 500 (unusual but tests override)
        var error = new ValidationError("bad input", "Validation", 500);

        // OUTPUT: Still Application — ValidationError overrides base logic
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Application);
    }

    [Test]
    public async Task Phase02_GoalError_AlwaysRuntime_Even400()
    {
        // INPUT: GoalError with status 400
        var error = new GoalError("goal not found", "NotFound", 400);

        // OUTPUT: Still Runtime — GoalError overrides base logic
        await Assert.That(error.Category).IsEqualTo(ErrorCategory.Runtime);
    }

    [Test]
    public async Task Phase02_ApplicationFormat_IsConcise()
    {
        // INPUT: Application error (400)
        var step = new Step { Index = 0, Text = "validate %email% is not empty" };
        var goal = new Goal { Name = "Start", Path = "Start.goal" };
        step.Goal = goal;
        var error = new ValidationError("Email address is required", step);

        // OUTPUT: unified format — full detail for all errors
        var output = error.Format();

        await Assert.That(output).Contains("Email address is required");
        await Assert.That(output).Contains("validate %email% is not empty");
        await Assert.That(output).Contains("Start.goal");
        await Assert.That(output).Contains("==================");
    }

    [Test]
    public async Task Phase02_RuntimeFormat_HasFullDetail()
    {
        // INPUT: Runtime error (500) with exception
        var step = new Step { Index = 0, Text = "read file data.txt" };
        var goal = new Goal { Name = "Start", Path = "Start.goal" };
        step.Goal = goal;
        var ex = new InvalidOperationException("Access denied");
        var error = new Error("Failed to read file", step, "FileError", 500)
        {
            Exception = ex
        };

        // OUTPUT: full detail with ======, reason, C# developer info
        var output = error.Format();

        await Assert.That(output).Contains("==================");
        await Assert.That(output).Contains("FileError(500)");
        await Assert.That(output).Contains("Failed to read file");
        await Assert.That(output).Contains("InvalidOperationException: Access denied");
    }

    // ================================================================
    // Phase 0.4 — Type Preservation (list handlers return explicit types)
    // ================================================================

    [Test]
    public async Task Phase04_ListType_IsPreserved()
    {
        // INPUT: Data.Ok with a list value and explicit list type
        var listValue = new global::app.modules.list.types.list
        {
            count = 3,
            value = new List<object?> { 1, 2, 3 }
        };
        var result = Data.Ok(listValue, R2.type.FromName("list"));

        // OUTPUT: Type is "list", not the CLR type name
        await Assert.That(result.Type).IsNotNull();
        await Assert.That(result.Type!.Value).IsEqualTo("list");
    }

    [Test]
    public async Task Phase04_ScalarType_AutoInferred()
    {
        // INPUT: Data.Ok with an int value (no explicit type)
        var result = Data.Ok(42);

        // OUTPUT: Type auto-inferred as "int"
        await Assert.That(result.Type).IsNotNull();
        await Assert.That(result.Type!.Value).IsEqualTo("int");
    }

    // ================================================================
    // Phase 0.5 — TString with %var% resolution
    // ================================================================

    [Test]
    public async Task Phase05_TString_NoResolver_ReturnsRaw()
    {
        // INPUT: TString with %var% but no resolver
        var ts = new R2.TString("hello %name%");

        // OUTPUT: raw template returned as-is
        await Assert.That(ts.ToString()).IsEqualTo("hello %name%");
    }

    [Test]
    public async Task Phase05_TString_WithResolver_ResolvesVariables()
    {
        // INPUT: TString with resolver (simulates Variables)
        var vars = new Dictionary<string, object?>
        {
            ["name"] = "John",
            ["age"] = 30
        };
        var ts = new R2.TString(
            "Hello %name%, you are %age% years old!",
            resolver: varName => vars.GetValueOrDefault(varName));

        // OUTPUT: variables resolved
        await Assert.That(ts.ToString()).IsEqualTo("Hello John, you are 30 years old!");
    }

    [Test]
    public async Task Phase05_TString_UnresolvedVar_KeptAsIs()
    {
        // INPUT: template with mix of known and unknown vars
        var vars = new Dictionary<string, object?> { ["name"] = "Alice" };
        var ts = new R2.TString(
            "%name% lives in %city%",
            resolver: varName => vars.GetValueOrDefault(varName));

        // OUTPUT: known resolved, unknown kept as %var%
        await Assert.That(ts.ToString()).IsEqualTo("Alice lives in %city%");
    }

    [Test]
    public async Task Phase05_TString_DotNotation()
    {
        // INPUT: dot notation variable like %user.name%
        var ts = new R2.TString(
            "Welcome %user.name%!",
            resolver: varName => varName == "user.name" ? "Bob" : null);

        // OUTPUT: dot notation resolved
        await Assert.That(ts.ToString()).IsEqualTo("Welcome Bob!");
    }

    [Test]
    public async Task Phase05_TString_UnclosedPercent_Safe()
    {
        // INPUT: unclosed % (e.g., "50% done")
        var ts = new R2.TString("50% done", resolver: _ => null);

        // OUTPUT: no crash, returned as-is
        await Assert.That(ts.ToString()).IsEqualTo("50% done");
    }

    [Test]
    public async Task Phase05_TString_ImplicitConversions()
    {
        // INPUT: implicit string → TString
        R2.TString ts = "hello";

        // OUTPUT: implicit TString → string
        string s = ts;
        await Assert.That(s).IsEqualTo("hello");
    }

    [Test]
    public async Task Phase05_TypeMapping_RegistersTString()
    {
        // INPUT: lookup "tstring" in TypeMapping
        var type = TypeMapping.GetType("tstring");

        // OUTPUT: resolves to App TString
        await Assert.That(type).IsEqualTo(typeof(R2.TString));

        // INPUT: reverse lookup
        var name = TypeMapping.GetTypeName(typeof(R2.TString));

        // OUTPUT: "tstring"
        await Assert.That(name).IsEqualTo("tstring");
    }

    [Test]
    public async Task Phase05_CultureInfo_DefaultsToInvariant()
    {
        // INPUT: new Engine
        await using var engine = new global::app.@this("/app");

        // OUTPUT: culture defaults to InvariantCulture
        await Assert.That(engine.Culture).IsEqualTo(System.Globalization.CultureInfo.InvariantCulture);
    }
}
