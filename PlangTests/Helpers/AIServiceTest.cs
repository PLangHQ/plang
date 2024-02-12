using PLang.Interfaces;
using PLang.Models;
using PLang.Utils.Extractors;

namespace PLangTests.Helpers
{
	public class AIServiceTest : ILlmService
	{
		public IContentExtractor Extractor { get { return new JsonExtractor(); } set { } }

		public Task<T?> Query<T>(LlmRequest question)
		{
			return Task.FromResult<T?>(default);
		}

		public Task<object?> Query(LlmRequest question, Type responseType)
		{
			return Task.FromResult<object?>(default);
		}
	}
}
