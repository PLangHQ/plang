using PLang.Errors;
using PLang.Models;
using PLang.Utils.Extractors;

namespace PLang.Interfaces
{
    public interface ILlmService
    {
        public IContentExtractor Extractor { get; set; }
		public abstract Task<(T?, IError?)> Query<T>(LlmRequest question);
		public abstract Task<(object?, IError?)> Query(LlmRequest question, Type responseType);

	}
}
