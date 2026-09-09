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

		protected override string ModelName(LlmRequest question) => "mercury-2.5";

		protected override string BuildRequestBody(LlmRequest question)
		{
			// Mercury spends most of a small budget on reasoning tokens (182 of 192 on a one word
			// answer), and max_tokens covers reasoning plus content, so plang's own maxLength can
			// leave nothing for the answer and the extractor gets an empty string.
			var maxTokens = Math.Max(question.maxLength, 8000);
			return $@"{{
		""model"":""{ModelName(question)}"",
		""reasoning_effort"":""low"",
		""temperature"":{question.temperature.ToString(CultureInfo.InvariantCulture)},
		""max_tokens"":{maxTokens},
		""messages"":{JsonConvert.SerializeObject(question.promptMessage)}
			}}";
		}
	}
}
