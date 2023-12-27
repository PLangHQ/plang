using PLang.Building.Model;
using PLang.Utils.Extractors;

namespace PLang.Interfaces
{
	public interface ILlmService
    {
        public IContentExtractor Extractor { get; set; }
        public abstract Task<T?> Query<T>(LlmQuestion question);
        public abstract Task<object?> Query(LlmQuestion question, Type responseType);

    }
}
