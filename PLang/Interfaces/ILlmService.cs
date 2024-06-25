using PLang.Errors;
using PLang.Models;
using PLang.Utils.Extractors;

namespace PLang.Interfaces
{

	public record CurrencyValue(string Currency, long Value, int DecimalPoint)
	{
		public double DecimalValue { get { return Value / Math.Pow(10, DecimalPoint); } }
	};


	public interface ILlmService
    {
        public IContentExtractor Extractor { get; set; }
		public abstract Task<(T?, IError?)> Query<T>(LlmRequest question);
		public abstract Task<(object?, IError?)> Query(LlmRequest question, Type responseType);

		public abstract Task<(object?, IError?)> GetBalance();

	}
}
