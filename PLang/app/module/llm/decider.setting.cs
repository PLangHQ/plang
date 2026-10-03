namespace app.module.llm;

public partial class decider
{
    /// <summary>
    /// The decider's own settings — <c>%!llm.decider.setting%</c>: where its questions go and the key they go with.
    /// <c>save %!llm.decider.setting%</c> keeps them on the actor's row.
    /// </summary>
    public sealed class setting : global::app.type.item.setting.@this
    {
        /// <summary>The key the decider is asked with — <c>%!llm.decider.setting.key%</c>; when none is saved, the
        /// <c>TYPESAFE_API_KEY</c> environment variable's. Never shown.</summary>
        [Out, Store, Sensitive] public global::app.type.item.text.@this Key { get; set; }
            = System.Environment.GetEnvironmentVariable("TYPESAFE_API_KEY") ?? "";

        /// <summary>Where the decider is asked — when none is saved, the <c>TYPESAFE_ENDPOINT</c> environment
        /// variable's, else the typesafe service's.</summary>
        [Out, Store] public global::app.type.item.text.@this Endpoint { get; set; }
            = System.Environment.GetEnvironmentVariable("TYPESAFE_ENDPOINT") is { Length: > 0 } endpoint
                ? endpoint : "https://api.typesafe.ai/v1/systemone";
    }
}
