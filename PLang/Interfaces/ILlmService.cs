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
		public abstract Task<(T? Response, IError? Error)> Query<T>(LlmRequest question) where T : class;
		public abstract Task<(object? Response, IError? Error)> Query(LlmRequest question, Type responseType);

		// One turn of a conversation with tools: the items come back untouched for the history, the
		// text and tool calls parsed for the loop. Query is for the builder (one schema, one answer).
		public abstract Task<(LlmChatResult? Result, IError? Error)> Chat(LlmChatRequest request);

	}
}
