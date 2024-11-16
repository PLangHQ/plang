using PLang.Errors;
using PLang.Models;
using PLang.Utils.Extractors;

namespace PLang.Interfaces;

public record CurrencyValue(string Currency, long Value, int DecimalPoint)
{
    public double DecimalValue => Value / Math.Pow(10, DecimalPoint);
}

public interface ILlmService
{
    public IContentExtractor Extractor { get; set; }
    public Task<(T?, IError?)> Query<T>(LlmRequest question) where T : class;
    public Task<(object?, IError?)> Query(LlmRequest question, Type responseType);

    public Task<(object?, IError?)> GetBalance();
}