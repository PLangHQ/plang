using System.Linq;
using Microsoft.CodeAnalysis;
using PLang.Generators.Discovery;
using ActionEmitter = PLang.Generators.Emission.Action.@this;

namespace PLang.Generators;

/// <summary>
/// PLang source generator entry point. Discovers [Action]-attributed partial classes,
/// builds an ActionClassInfo (Discovery/), and delegates emission to Emission/Action/.
/// Per-property emission lives on the ActionProperty hierarchy under Emission/Property/.
/// </summary>
[Generator]
public class LazyParamsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var actionDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => Discovery.@this.IsActionPartialClass(node),
                transform: static (ctx, _) => Discovery.@this.GetActionClassInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(actionDeclarations, static (spc, info) =>
        {
            if (info is null) return;
            var source = ActionEmitter.Emit(info);
            spc.AddSource(SanitizeHintName($"{info.FullName}.Action.g.cs"), source);
        });
    }

    private static string SanitizeHintName(string hintName) => hintName.Replace("@", "");
}
