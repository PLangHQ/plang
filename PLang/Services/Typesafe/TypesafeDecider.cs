using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using System.Text;

namespace PLang.Services.Typesafe
{
	// Typesafe.ai "System One" is not a chat model: it answers typed questions against a state and
	// returns a choice with a probability over the options. That is exactly the module and method
	// decisions the builder makes for every step, so it lives behind IBuilderDecider rather than
	// ILlmService. https://docs.typesafe.ai/api
	public class TypesafeDecider : IBuilderDecider
	{
		private readonly ISettings settings;
		private readonly ILogger logger;

		private const string Url = "https://api.typesafe.ai/v1/systemone";
		private const string Model = "jev-latest";
		private const string SettingKey = "TypeSafeApiKey";

		private record Answer(string Choice, double Confidence, Dictionary<string, double> Probabilities);

		public TypesafeDecider(ISettings settings, ILogger logger)
		{
			this.settings = settings;
			this.logger = logger;
		}

		// Same home as every other key in plang: %Settings.TypeSafeApiKey%, kept in system.sqlite.
		// A missing key must never prompt: settings.Get throws MissingSettingsException, which the
		// engine turns into the ask-user flow, and that has no place inside a build (a system app
		// built in another settings scope hit exactly this). The miss is swallowed here and Choose
		// turns it into an error the builder falls back from.
		private string? GetKey()
		{
			try
			{
				return settings.Get<string>(this.GetType(), SettingKey, "", "Type in API key for Typesafe.ai");
			}
			catch (Exception)
			{
				return null;
			}
		}

		public async Task<(ModuleChoice? Choice, IError? Error)> ChooseModule(string stepText, Dictionary<string, string> modules)
		{
			var (answer, error) = await Choose(stepText, "module",
				"This is one step of plang code. Which plang module implements what this step does?", modules);
			if (error != null || answer == null) return (null, error);

			return (new ModuleChoice(answer.Choice, answer.Confidence, answer.Probabilities), null);
		}

		public async Task<(MethodChoice? Choice, IError? Error)> ChooseMethod(string stepText, string module, Dictionary<string, string> methods)
		{
			var (answer, error) = await Choose(stepText, "method",
				$"This is one step of plang code that uses the module {module}. Which method of the module does this step call?", methods);
			if (error != null || answer == null) return (null, error);

			return (new MethodChoice(answer.Choice, answer.Confidence, answer.Probabilities), null);
		}

		// One Choice question: the step is the state, the options are the criteria, and the answer
		// is the option key plus how sure the model is.
		private async Task<(Answer? Answer, IError? Error)> Choose(string state, string questionKey, string instructions, Dictionary<string, string> criteria)
		{
			if (criteria == null || criteria.Count == 0)
			{
				return (null, new ServiceError($"No options to choose from for '{questionKey}'", this.GetType()));
			}

			var key = GetKey();
			if (string.IsNullOrEmpty(key))
			{
				return (null, new ServiceError("TypeSafeApiKey is not set in this app's settings (%Settings.TypeSafeApiKey%)", this.GetType(), Key: "MissingTypeSafeApiKey"));
			}

			var body = new Dictionary<string, object>
			{
				["state"] = state,
				["model"] = Model,
				["questions"] = new Dictionary<string, object>
				{
					[questionKey] = new Dictionary<string, object>
					{
						["type"] = "choice",
						["instructions"] = instructions,
						["criteria"] = criteria
					}
				}
			};

			using var httpClient = new HttpClient();
			httpClient.Timeout = TimeSpan.FromMinutes(2);
			using var request = new HttpRequestMessage(HttpMethod.Post, Url);
			request.Headers.UserAgent.ParseAdd("plang v0.1");
			request.Headers.Add("Authorization", $"Bearer {key}");
			request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

			string responseBody;
			try
			{
				using var response = await httpClient.SendAsync(request);
				responseBody = await response.Content.ReadAsStringAsync();
				if (!response.IsSuccessStatusCode)
				{
					return (null, new ServiceError($"Typesafe returned {(int)response.StatusCode}: {responseBody}", this.GetType(), StatusCode: (int)response.StatusCode));
				}
			}
			catch (Exception ex)
			{
				return (null, new ServiceError($"Could not reach Typesafe: {ex.Message}", this.GetType()));
			}

			var answer = JObject.Parse(responseBody)["answers"]?[questionKey];
			var choice = answer?["choice"]?.ToString();
			if (string.IsNullOrEmpty(choice))
			{
				return (null, new ServiceError($"Typesafe gave no '{questionKey}' choice: {responseBody}", this.GetType()));
			}

			var confidence = answer?["confidence"]?.Value<double>() ?? 0;
			var probabilities = answer?["probabilities"]?.ToObject<Dictionary<string, double>>() ?? new();

			logger.LogDebug("Typesafe chose {Choice} for {Question} with confidence {Confidence}", choice, questionKey, confidence);
			return (new Answer(choice, confidence, probabilities), null);
		}
	}
}
