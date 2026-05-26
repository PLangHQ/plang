using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Reflection;
using global::app.types.path;
using FilePath = global::app.types.path.file.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// The <c>[PathScheme]</c> marker attribute.
/// </summary>
public class PathSchemeAttributeTests
{
    [Test] public async Task PathSchemeAttribute_HasClassTarget_AllowMultiple()
    {
        var usage = typeof(PathSchemeAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        await Assert.That(usage).IsNotNull();
        await Assert.That(usage!.ValidOn).IsEqualTo(AttributeTargets.Class);
        await Assert.That(usage.AllowMultiple).IsTrue();
    }

    [Test] public async Task PathSchemeAttribute_ExposesSingleSchemeString()
    {
        var props = typeof(PathSchemeAttribute)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        await Assert.That(props.Length).IsEqualTo(1);
        await Assert.That(props[0].Name).IsEqualTo("Scheme");
        await Assert.That(props[0].PropertyType).IsEqualTo(typeof(string));

        var ctor = typeof(PathSchemeAttribute)
            .GetConstructor(new[] { typeof(string) });
        await Assert.That(ctor).IsNotNull();

        var instance = new PathSchemeAttribute("ftp");
        await Assert.That(instance.Scheme).IsEqualTo("ftp");
    }

    [Test] public async Task Reflection_FindsBothSchemes_OnMultiplyDecoratedClass()
    {
        var attrs = typeof(TwoSchemeFixture)
            .GetCustomAttributes<PathSchemeAttribute>(inherit: false)
            .Select(a => a.Scheme)
            .OrderBy(s => s)
            .ToArray();
        await Assert.That(attrs.Length).IsEqualTo(2);
        await Assert.That(attrs[0]).IsEqualTo("alpha");
        await Assert.That(attrs[1]).IsEqualTo("beta");
    }

    [Test] public async Task FilePath_Carries_PathSchemeFile_Attribute()
    {
        var attrs = typeof(FilePath)
            .GetCustomAttributes<PathSchemeAttribute>(inherit: false)
            .Select(a => a.Scheme)
            .ToArray();
        await Assert.That(attrs).Contains("file");
    }

    [Test] public async Task SchemeHandler_Exposes_PublicSingleStringConstructor()
    {
        var ctor = typeof(FilePath).GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(c =>
            {
                var ps = c.GetParameters();
                return ps.Length >= 1
                    && ps[0].ParameterType == typeof(string)
                    && ps.Skip(1).All(p => p.IsOptional);
            });
        await Assert.That(ctor).IsNotNull();
    }

    [PathScheme("alpha"), PathScheme("beta")]
    private sealed class TwoSchemeFixture { }
}
