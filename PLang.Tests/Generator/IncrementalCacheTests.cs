using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PLang.Generators;
using PLang.Generators.Discovery;

using PropertyBase = PLang.Generators.Emission.Property.@this;
using DataProperty = PLang.Generators.Emission.Property.Data.@this;

namespace PLang.Tests.Generator;

// Contract tests for IIncrementalGenerator value equality.
// The IIncrementalGenerator pipeline caches by structural equality. If ActionClassInfo
// (the carrier passed across stages) lacked value equality, the cache would always miss
// — every keystroke would re-emit every handler. These tests pin the contract that two
// structurally identical ActionClassInfo instances compare equal AND share a hash code.
//
// Codeanalyzer Finding 1: my prior test only checked for IPropertySymbol leaks; it did
// not check that the cache actually hits. These tests fill that gap.
//
// Known limitation (tester v3 #4): the three `PipelineCache_*` tests below fail when
// the suite is run with `dotnet run --project PLang.Tests -- --coverage`. The Roslyn
// `CSharpGeneratorDriver` with `trackIncrementalGeneratorSteps:true` interacts poorly
// with coverage instrumentation — coverage hooks wrap generator pipeline lambdas and
// strip the tracked-step labels, so `runResult.TrackedSteps` does not contain the
// `ActionInfo` / `ActionInfoFiltered` keys, and the tests fail with
// `KeyNotFoundException`. The tests pass cleanly without `--coverage`. Do not gate
// CI on coverage of this file.

public class IncrementalCacheTests
{
    private static ActionClassInfo MakeInfo(string name = "Handler",
        params PropertyBase[] props)
        => new(
            Namespace: "app.module.test",
            ClassName: name,
            FullName: $"app.module.test.{name}",
            ImplementsIContext: true,
            ImplementsIChannel: false,
            ImplementsIAction: true,
            ImplementsIStep: false,
            ImplementsIStatic: false,
            Properties: new EquatableArray<PropertyBase>(props),
            IEventPropertyNames: EquatableArray<string>.Empty,
            HasAnyIsNotNull: false,
            IsNotNullProperties: EquatableArray<string>.Empty,
            Diagnostics: EquatableArray<DiagnosticInfo>.Empty);

    [Test]
    public async Task ActionClassInfo_StructurallyIdentical_AreEqual()
    {
        var a = MakeInfo();
        var b = MakeInfo();
        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task ActionClassInfo_DifferentClassName_AreNotEqual()
    {
        var a = MakeInfo("HandlerA");
        var b = MakeInfo("HandlerB");
        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task ActionClassInfo_StructurallyIdenticalProperties_AreEqual()
    {
        var propsA = new PropertyBase[]
        {
            new DataProperty("First", "global::app.data.@this<string>", IsNullable: false, IsPlainData: false, InnerType: "string", DefaultValue: null, IsSensitive: false, IsRawNameResolvable: false),
            new DataProperty("Second", "global::app.data.@this<int>", IsNullable: false, IsPlainData: false, InnerType: "int", DefaultValue: null, IsSensitive: false, IsRawNameResolvable: false),
        };
        var propsB = new PropertyBase[]
        {
            new DataProperty("First", "global::app.data.@this<string>", IsNullable: false, IsPlainData: false, InnerType: "string", DefaultValue: null, IsSensitive: false, IsRawNameResolvable: false),
            new DataProperty("Second", "global::app.data.@this<int>", IsNullable: false, IsPlainData: false, InnerType: "int", DefaultValue: null, IsSensitive: false, IsRawNameResolvable: false),
        };

        var a = MakeInfo("X", propsA);
        var b = MakeInfo("X", propsB);

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task ActionClassInfo_DifferentPropertyOrder_AreNotEqual()
    {
        var p1 = new DataProperty("A", "global::app.data.@this<string>", false, false, "string", null, false, false);
        var p2 = new DataProperty("B", "global::app.data.@this<int>", false, false, "int", null, false, false);

        var a = MakeInfo("X", p1, p2);
        var b = MakeInfo("X", p2, p1);

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task ActionClassInfo_DifferentMarkers_AreNotEqual()
    {
        var a = MakeInfo();
        var b = a with { ImplementsIChannel = true };
        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task EquatableArray_TwoArraysWithSameElements_Equal()
    {
        var x = new EquatableArray<string>(new[] { "a", "b", "c" });
        var y = new EquatableArray<string>(new[] { "a", "b", "c" });
        await Assert.That(x).IsEqualTo(y);
        await Assert.That(x.GetHashCode()).IsEqualTo(y.GetHashCode());
    }

    [Test]
    public async Task EquatableArray_DifferentElements_NotEqual()
    {
        var x = new EquatableArray<string>(new[] { "a", "b" });
        var y = new EquatableArray<string>(new[] { "a", "c" });
        await Assert.That(x).IsNotEqualTo(y);
    }

    [Test]
    public async Task EquatableArray_DifferentOrder_NotEqual()
    {
        var x = new EquatableArray<string>(new[] { "a", "b" });
        var y = new EquatableArray<string>(new[] { "b", "a" });
        await Assert.That(x).IsNotEqualTo(y);
    }

    [Test]
    public async Task EquatableArray_EmptyAndDefault_Equal()
    {
        var x = new EquatableArray<string>(Array.Empty<string>());
        var y = EquatableArray<string>.Empty;
        await Assert.That(x).IsEqualTo(y);
    }

    // ====================================================================
    // Pipeline-driven cache-hit tests.
    //
    // codeanalyzer/v2 (#39) flagged that the carrier-equality tests above are not enough:
    // the IIncrementalGenerator pipeline contract is whether the value equality actually
    // causes downstream stages to skip work on the second run. These tests drive the
    // generator through CSharpGeneratorDriver with trackIncrementalGeneratorSteps:true
    // and inspect the TrackedSteps reasons directly.
    //
    // Regression these tests catch:
    //   If ActionClassInfo went back to `sealed class` with reference equality (the v1
    //   carrier), the second run's ActionInfo step would report Modified for every
    //   re-evaluated input. These tests would fail with that change.
    // ====================================================================

    private const string MinimalSource = """
        using System;
        namespace app.module {
            public class ActionAttribute : Attribute {}
            public class CodeAttribute : Attribute {}
            public interface IContext {}
            public interface IChannel {}
            public interface IAction {}
            public interface IStep {}
            public interface IStatic {}
            public interface IEvent {}
            public interface ICodeGenerated {}
        }
        namespace app.Data {
            public partial class @this {
                public static @this Ok() => null!;
                public static @this Ok(object? v) => null!;
                public static @this Ok(object? v, Type? t) => null!;
                public static @this NotFound(string n) => null!;
                public static @this FromError(object e) => null!;
                public T? As<T>(object? ctx) => default;
                public class Type {}
            }
            public partial class @this<T> : @this {}
        }
        namespace app {
            public partial class @this {}
            namespace Actor.Context { public partial class @this {} }
            namespace Errors {
                public interface IError {}
                public class ParamSnapshot { public string? Name; public string? DeclaredType; public object? PrValue; public object? PrType; public object? FinalValue; public bool WasAccessed; }
            }
            namespace Goals.Goal {
                public partial class @this {
                    public class GoalCall { public object? Action; }
                    public partial class Steps {
                        public partial class Step {
                            public partial class Actions {
                                public partial class Action {
                                    public partial class @this {
                                        public Steps.Step.@this? Step;
                                        public System.Collections.Generic.List<data.@this>? Parameters;
                                        public System.Collections.Generic.List<data.@this>? Defaults;
                                        public data.@this? GetParameter(string name, Actor.Context.@this ctx) => null;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            namespace CallStack {
                public class CallFrame {}
            }
        }

        namespace app.Test {
            [app.module.Action]
            public partial class TestHandler {
                public partial app.data.@this<string> Foo { get; init; }
            }
        }
        """;

    private static CSharpCompilation CreateCompilation(string source, string? assemblyName = "TestAssembly")
    {
        var refs = new[]
        {
            // System.Runtime — needed for object, string, etc.
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.IsExternalInit).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.RegularExpressions.Regex).Assembly.Location),
        };
        // System.Private.CoreLib + netstandard.dll — fold in everything object touches.
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator);
        var allRefs = new List<MetadataReference>(refs);
        if (trustedAssemblies != null)
        {
            foreach (var path in trustedAssemblies)
            {
                if (string.IsNullOrEmpty(path)) continue;
                if (path.EndsWith("System.Runtime.dll", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith("netstandard.dll", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith("System.Collections.dll", StringComparison.OrdinalIgnoreCase))
                {
                    allRefs.Add(MetadataReference.CreateFromFile(path));
                }
            }
        }

        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        return CSharpCompilation.Create(
            assemblyName ?? "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
            allRefs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static GeneratorDriver CreateDriver()
    {
        var generator = new PLang.Generators.@this().AsSourceGenerator();
        return CSharpGeneratorDriver.Create(
            new[] { generator },
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));
    }

    [Test]
    public async Task PipelineCache_RerunWithUnchangedSyntax_StepOutputsAreCachedOrUnchanged()
    {
        var compilation = CreateCompilation(MinimalSource);
        var driver = CreateDriver();

        // First run.
        driver = driver.RunGenerators(compilation);

        // Second run: same syntax, plus an unrelated tree the predicate ignores.
        // If ActionClassInfo were not value-equal, the ActionInfoFiltered step would
        // re-execute and report Modified. With value-equal carrier, it stays Unchanged/Cached.
        var compilation2 = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("namespace Unrelated; public class X {}",
                new CSharpParseOptions(LanguageVersion.Preview)));
        driver = driver.RunGenerators(compilation2);

        var result = driver.GetRunResult().Results[0];
        await Assert.That(result.TrackedSteps).ContainsKey(PLang.Generators.@this.ActionInfoFilteredTrackingName);

        var infoSteps = result.TrackedSteps[PLang.Generators.@this.ActionInfoFilteredTrackingName];
        await Assert.That(infoSteps.Length).IsGreaterThan(0);

        // Every output of every step should be Cached or Unchanged on the second run.
        // Modified would mean the carrier value changed despite no semantic change — the
        // exact failure mode the v1 sealed-class regression produced.
        foreach (var step in infoSteps)
        {
            foreach (var (_, reason) in step.Outputs)
            {
                var ok = reason == IncrementalStepRunReason.Cached
                      || reason == IncrementalStepRunReason.Unchanged;
                await Assert.That(ok).IsTrue()
                    .Because($"Expected Cached or Unchanged, got {reason}");
            }
        }
    }

    [Test]
    public async Task PipelineCache_RerunWithUnchangedSyntax_UnfilteredStepOutputsAreCachedOrUnchanged()
    {
        // Pre-Where step (ActionInfoTrackingName). Strictly stronger than the post-Where
        // contract: the post-Where step's outputs can stay Cached even if the upstream
        // transform produced a fresh-but-value-equal ActionClassInfo, because Where only
        // forwards on value-change. The pre-Where step would catch transform-step
        // instability where `GetActionClassInfo` regressed to non-deterministic capture.
        var compilation = CreateCompilation(MinimalSource);
        var driver = CreateDriver();

        driver = driver.RunGenerators(compilation);

        var compilation2 = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("namespace Unrelated; public class X {}",
                new CSharpParseOptions(LanguageVersion.Preview)));
        driver = driver.RunGenerators(compilation2);

        var result = driver.GetRunResult().Results[0];
        await Assert.That(result.TrackedSteps).ContainsKey(PLang.Generators.@this.ActionInfoTrackingName);

        var infoSteps = result.TrackedSteps[PLang.Generators.@this.ActionInfoTrackingName];
        await Assert.That(infoSteps.Length).IsGreaterThan(0);

        foreach (var step in infoSteps)
        {
            foreach (var (_, reason) in step.Outputs)
            {
                var ok = reason == IncrementalStepRunReason.Cached
                      || reason == IncrementalStepRunReason.Unchanged;
                await Assert.That(ok).IsTrue()
                    .Because($"Pre-Where step expected Cached or Unchanged, got {reason}");
            }
        }
    }

    [Test]
    public async Task PipelineCache_ActionClassChanged_StepOutputIsModified()
    {
        // Sanity-check: when the [Action] partial class actually changes, the cache
        // correctly invalidates and the step output reports Modified (not Cached).
        // Without this assertion, a vacuously-passing cache test (always Cached) would
        // not be caught. This is the negative space of the cache-hit contract.
        var compilation = CreateCompilation(MinimalSource);
        var driver = CreateDriver();

        driver = driver.RunGenerators(compilation);

        // Replace TestHandler's property type so ActionClassInfo's Properties array changes.
        var modifiedSource = MinimalSource.Replace(
            "public partial app.data.@this<string> Foo { get; init; }",
            "public partial app.data.@this<int> Foo { get; init; }");
        var compilation2 = CreateCompilation(modifiedSource);
        driver = driver.RunGenerators(compilation2);

        var result = driver.GetRunResult().Results[0];
        var infoSteps = result.TrackedSteps[PLang.Generators.@this.ActionInfoFilteredTrackingName];
        var anyModified = infoSteps.SelectMany(s => s.Outputs)
            .Any(o => o.Reason == IncrementalStepRunReason.Modified
                   || o.Reason == IncrementalStepRunReason.New);
        await Assert.That(anyModified).IsTrue()
            .Because("Changing the partial property type must invalidate the cache");
    }
}
