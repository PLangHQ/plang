namespace app.modules.llm.code;

/// <summary>
/// DeepSeek provider — the built-in default <see cref="ILlm"/>. Speaks the
/// OpenAI chat-completions wire format, so everything (message formatting,
/// tool loop, caching, streaming, validation) is inherited from
/// <see cref="OpenAiCompatible"/>. Only the endpoint, env vars, default
/// model, and price book change.
/// Setting keys stay on the generic <c>llm.*</c> prefix (since DeepSeek is
/// the sole built-in provider); env vars are vendor-specific
/// (<c>DEEPSEEK_API_*</c>) so they match DeepSeek's own docs verbatim.
/// </summary>
public sealed class DeepSeek : OpenAiCompatible
{
    public override string Name { get; init; } = "DeepSeek";

    protected override string DefaultEndpoint => "https://api.deepseek.com/v1/chat/completions";
    protected override string EndpointEnvVar  => "DEEPSEEK_API_ENDPOINT";
    protected override string ApiKeyEnvVar    => "DEEPSEEK_API_KEY";
    protected override string DefaultModel    => "deepseek-v4-flash";

    // USD per 1M tokens. Longest matching prefix wins; missing model → null cost.
    // Prices as of 2026-05.
    private static readonly (string prefix, decimal input, decimal cached, decimal output)[] DeepSeekPricing = new[]
    {
        ("deepseek", 0.14m, 0.0028m, 0.28m),
    };

    protected override (string prefix, decimal input, decimal cached, decimal output)[] PricingTable => DeepSeekPricing;
}
