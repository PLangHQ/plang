namespace app.module.cache.type;

/// <summary>Whether an answer kept from before is used: an llm query asked again, a build's answers.</summary>
[global::app.Attributes.PlangType("cache")]
public enum cache
{
    use,
    skip,
}
