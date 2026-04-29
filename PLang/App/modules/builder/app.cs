using App.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[System.ComponentModel.Description("Load the app-level build context from a directory path")]
[Action("app")]
public partial class app : IContext
{
    [Default(".")]
    public partial Data.@this<string> Path { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data.@this> Run() => await Builder.App(this);
}
