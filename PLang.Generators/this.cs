using System.Linq;
using Microsoft.CodeAnalysis;
using PLang.Generators.Diagnostics;
using PLang.Generators.Discovery;
using ActionEmitter = PLang.Generators.Emission.Action.@this;

namespace PLang.Generators;

/// <summary>
/// PLang source generator entry point. Discovers [Action]-attributed partial classes,
/// builds an ActionClassInfo (Discovery/), and delegates emission to Emission/Action/.
/// Per-property emission lives on the ActionProperty hierarchy under Emission/Property/.
/// </summary>
[Generator]
public class @this : IIncrementalGenerator
{
    /// <summary>
    /// Tracking names exposed for incremental cache regression tests
    /// (see PLang.Tests.Generator.IncrementalCacheTests).
    /// </summary>
    public const string ActionInfoTrackingName = "ActionInfo";
    public const string ActionInfoFilteredTrackingName = "ActionInfoFiltered";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var actionDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => Discovery.@this.IsActionPartialClass(node),
                transform: static (ctx, _) => Discovery.@this.GetActionClassInfo(ctx))
            .WithTrackingName(ActionInfoTrackingName)
            .Where(static info => info is not null)
            .WithTrackingName(ActionInfoFilteredTrackingName);

        context.RegisterSourceOutput(actionDeclarations, static (spc, info) =>
        {
            if (info is null) return;

            // Emit any v4-contract diagnostics first (raw-scalar non-Data<T> properties).
            // Carries the full identifier span so IDE squiggles underline the property name,
            // not a synthetic one-character mark.
            foreach (var d in info.Diagnostics)
            {
                var location = !string.IsNullOrEmpty(d.FilePath)
                    ? Location.Create(d.FilePath,
                        Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(0, 0),
                        new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                            new Microsoft.CodeAnalysis.Text.LinePosition(d.StartLine, d.StartCharacter),
                            new Microsoft.CodeAnalysis.Text.LinePosition(d.EndLine, d.EndCharacter)))
                    : Location.None;
                spc.ReportDiagnostic(Diagnostic.Create(
                    Discovery.@this.RawScalarPropertyDescriptor,
                    location,
                    d.PropertyName, d.ClassName));
            }

            var source = ActionEmitter.Emit(info);
            spc.AddSource(SanitizeHintName($"{info.FullName}.Action.g.cs"), source);
        });

        Plng002.Register(context);
        Plng003.Register(context);
        Plng004.Register(context);
    }

    private static string SanitizeHintName(string hintName) => hintName.Replace("@", "");
}
