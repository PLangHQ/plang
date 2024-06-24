using PLang.Errors;
using PLang.Interfaces;
using PLang.Models;
using PLang.Utils.Extractors;

namespace PLangTests.Helpers
{
	public class AIServiceTest : ILlmService
	{
		public IContentExtractor Extractor { get { return new JsonExtractor(); } set { } }

		public async Task<(object?, IError?)> GetBalance()
		{
			return (null, null);
		}

		public Task<(T?, IError?)> Query<T>(LlmRequest question)
		{
			return Task.FromResult<(T?, IError)>(default);
		}

		public Task<(object?, IError?)> Query(LlmRequest question, Type responseType)
		{
			return Task.FromResult<(object?, IError)>(default);
		}
	}
}
