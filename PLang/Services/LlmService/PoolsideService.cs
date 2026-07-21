using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Services.OpenAi;
using System;
using System.Globalization;

namespace PLang.Services.Poolside
{
	// Poolside exposes an OpenAI-compatible chat/completions endpoint, so we reuse OpenAiService
	// and only change the endpoint, the settings key (its own API key slot), and the request body
	// (fixed model, top_p must be > 0, and it rejects the penalty params at 0).
	// Selected with --llmservice=poolside. Key is stored under this type + "PoolsideKey".
	public class PoolsideService : OpenAiService
	{
		public PoolsideService(ISettings settings, ILogger logger, LlmCaching llmCaching, PLangAppContext context)
			: base(settings, logger, llmCaching, context)
		{
			url = "https://inference.poolside.ai/v1/chat/completions";
			settingKey = "PoolsideKey";
		}

		protected override string BuildRequestBody(LlmRequest question)
		{
			// poolside rejects top_p <= 0 (plang sends 0 for deterministic builds; temperature=0 keeps it greedy)
			var topP = Math.Max(0.01, question.top_p);
			return $@"{{
		""model"":""poolside/laguna-xs-2.1"",
		""temperature"":{question.temperature.ToString(CultureInfo.InvariantCulture)},
		""max_tokens"":{question.maxLength},
		""top_p"":{topP.ToString(CultureInfo.InvariantCulture)},
		""messages"":{JsonConvert.SerializeObject(question.promptMessage)}
			}}";
		}
	}
}
