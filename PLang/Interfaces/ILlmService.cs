﻿using PLang.Errors;
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

	}
}
