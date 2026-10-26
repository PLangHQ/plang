namespace app.module.llm;

public partial class decider
{
    /// <summary>
    /// The decider's own settings — <c>%!llm.decider.setting%</c>: where its questions go and the key they go with.
    /// <c>save %!llm.decider.setting%</c> keeps them on the actor's row.
    /// </summary>
    // A nested settings class (declared inside the decider it configures): it says its own name —
    // the owner rule's — so discovery's existing gate admits it and it never collides with llm's own setting.
    [global::app.Attributes.PlangType("app.module.llm.decider.setting")]
    public sealed class setting : global::app.type.item.setting.@this, global::app.module.llm.setting.IKey
    {
        /// <summary>Born holding its type — the app's list hands it its own class.</summary>
        public setting(global::app.type.@this type) : base(type) { }

        /// <summary>A settings class is a type but never LLM vocabulary — kept out of the catalog.</summary>
        public static bool Internal => true;

        /// <summary>The key the decider is asked with — <c>%!llm.decider.setting.key%</c>; when none is saved, the
        /// <c>TYPESAFE_API_KEY</c> environment variable's. Never shown.</summary>
        [Out, Store, Sensitive] public global::app.type.item.text.@this Key { get; set; }
            = System.Environment.GetEnvironmentVariable("TYPESAFE_API_KEY") ?? "";

        /// <summary>What a build asks for when no key is held.</summary>
        global::app.type.item.text.@this global::app.module.llm.setting.IKey.Question
            => "Building needs a TypeSafe key for the decider. Paste it:";

        /// <summary>No key, and no one gave one.</summary>
        global::app.error.@this global::app.module.llm.setting.IKey.Missing => new(
            "Building needs a TypeSafe key for the decider, and no one gave one: set %!llm.decider.setting.key% or the TYPESAFE_API_KEY environment variable.",
            "MissingDeciderKey", 401);

        /// <summary>Where the decider is asked — when none is saved, the <c>TYPESAFE_ENDPOINT</c> environment
        /// variable's, else the typesafe service's.</summary>
        [Out, Store] public global::app.type.item.text.@this Endpoint { get; set; }
            = System.Environment.GetEnvironmentVariable("TYPESAFE_ENDPOINT") is { Length: > 0 } endpoint
                ? endpoint : "https://api.typesafe.ai/v1/systemone";
    }
}
