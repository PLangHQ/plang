using Newtonsoft.Json;
using PLang.Interfaces;
using PLang.Models;

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

		public string GetStringToHash(LlmRequest question)
		{
			return question.type + JsonConvert.SerializeObject(question.promptMessage).ComputeHash() + question.model + question.maxLength + question.top_p + question.frequencyPenalty + question.presencePenalty + question.temperature;
		}
		
		public LlmRequest? GetCachedQuestion(LlmRequest question)
		{
			var hash = GetStringToHash(question).ComputeHash();

			return settings.GetLlmRequest(hash);

		}

		public void SetCachedQuestion(LlmRequest question)
		{
			var hash = GetStringToHash(question).ComputeHash();
			settings.SetLlmQuestion(hash, question);
		}
	}

}
