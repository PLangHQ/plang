using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Interfaces;

namespace PLang.Utils
{
	public class CacheHelper
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;

		public CacheHelper(IPLangFileSystem fileSystem, ISettings settings)
		{
			this.fileSystem = fileSystem;
			this.settings = settings;
		}

		public string GetStringToHash(LlmQuestion question)
		{
			return question.type + question.system + question.assistant + question.question + question.model;
		}
		public LlmQuestion? GetCachedQuestion(LlmQuestion question)
		{
			var hash = GetStringToHash(question).ComputeHash();

			return settings.GetLlmQuestion(hash);

		}
		public void SetCachedQuestion(LlmQuestion question)
		{
			var hash = GetStringToHash(question).ComputeHash();
			settings.SetLlmQuestion(hash, question);
		}
	}

}
