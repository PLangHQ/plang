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
	// returns a choice with a probability over the options. That is exactly the module decision the
	// builder makes for every step, so it lives behind IBuilderDecider rather than ILlmService.
	// https://docs.typesafe.ai/api
	public class TypesafeDecider : IBuilderDecider
	{
		private readonly ISettings settings;
		private readonly ILogger logger;

		private const string Url = "https://api.typesafe.ai/v1/systemone";
		private const string Model = "jev-latest";
		private const string SettingKey = "TypeSafeApiKey";

		public TypesafeDecider(ISettings settings, ILogger logger)
		{
			this.settings = settings;
			this.logger = logger;
		}

		// Same home as every other key in plang: %Settings.TypeSafeApiKey%, kept in system.sqlite.
		private string? GetKey()
		{
			return settings.Get<string>(this.GetType(), SettingKey, "", "Type in API key for Typesafe.ai");
		}

		public async Task<(ModuleChoice? Choice, IError? Error)> ChooseModule(string stepText, Dictionary<string, string> modules)
		{
			if (modules == null || modules.Count == 0)
			{
				return (null, new ServiceError("No modules to choose from", this.GetType()));
			}

			var body = new Dictionary<string, object>
			{
				["state"] = stepText,
				["model"] = Model,
				["questions"] = new Dictionary<string, object>
				{
					["module"] = new Dictionary<string, object>
					{
						["type"] = "choice",
						["instructions"] = "This is one step of plang code. Which plang module implements what this step does?",
						["criteria"] = modules
					}
				}
			};

			using var httpClient = new HttpClient();
			httpClient.Timeout = TimeSpan.FromMinutes(2);
			using var request = new HttpRequestMessage(HttpMethod.Post, Url);
			request.Headers.UserAgent.ParseAdd("plang v0.1");
			request.Headers.Add("Authorization", $"Bearer {GetKey()}");
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

			var answer = JObject.Parse(responseBody)["answers"]?["module"];
			var choice = answer?["choice"]?.ToString();
			if (string.IsNullOrEmpty(choice))
			{
				return (null, new ServiceError("Typesafe gave no choice: " + responseBody, this.GetType()));
			}

			var confidence = answer?["confidence"]?.Value<double>() ?? 0;
			var probabilities = answer?["probabilities"]?.ToObject<Dictionary<string, double>>() ?? new();

			logger.LogDebug("Typesafe chose {Module} with confidence {Confidence}", choice, confidence);
			return (new ModuleChoice(choice, confidence, probabilities), null);
		}
	}
}
