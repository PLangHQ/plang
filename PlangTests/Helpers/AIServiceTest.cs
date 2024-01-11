using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Utils.Extractors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLangTests.Helpers
{
    public class AIServiceTest : ILlmService
	{
		public IContentExtractor Extractor { get { return new JsonExtractor(); } set { } }

		public Task<T?> Ask<T>(LlmQuestion question)
		{
			return default;
		}

		public Task<T?> Query<T>(LlmQuestion question) 
		{
			return Task.FromResult<T?>(default);
		}

		public Task<object?> Query(LlmQuestion question, Type responseType)
		{
			return Task.FromResult<object?>(default);
		}

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
