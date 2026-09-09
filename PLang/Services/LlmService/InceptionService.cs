using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Services.OpenAi;
using System.Globalization;

namespace PLang.Services.Inception
{
	// Inception Labs (Mercury) is a diffusion LLM behind an OpenAI-compatible chat/completions
	// endpoint, so this is the same shape as PoolsideService: reuse OpenAiService and change only
	// the endpoint, the settings key and the request body.
	// Selected with --llmservice=inception. Key is stored under this type + "InceptionKey".
	public class InceptionService : OpenAiService
	{
		public InceptionService(ISettings settings, ILogger logger, LlmCaching llmCaching, PLangAppContext context)
			: base(settings, logger, llmCaching, context)
		{
			url = "https://api.inceptionlabs.ai/v1/chat/completions";
			settingKey = "InceptionKey";
		}

		protected override string BuildRequestBody(LlmRequest question)
		{
			return $@"{{
		""model"":""mercury-2.5"",
		""reasoning_effort"":""low"",
		""temperature"":{question.temperature.ToString(CultureInfo.InvariantCulture)},
		""max_tokens"":{question.maxLength},
		""messages"":{JsonConvert.SerializeObject(question.promptMessage)}
			}}";
		}
	}
}
