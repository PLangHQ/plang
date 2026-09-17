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
	// returns a choice with a probability over the options. That is exactly the module, method and
	// parameter decisions the builder makes for every step, so it lives behind IBuilderDecider
	// rather than ILlmService. Several questions go in one request and are answered together.
	// https://docs.typesafe.ai/api
	public class TypesafeDecider : IBuilderDecider
	{
		private readonly ISettings settings;
		private readonly ILogger logger;

		// One client for the process. A client per request opens a connection pool per request and
		// leaves the sockets in TIME_WAIT, which a parallel build would run into.
		private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(2) };

		private const string Url = "https://api.typesafe.ai/v1/systemone";
		private const string Model = "jev-latest";
		private const string SettingKey = "TypeSafeApiKey";

		public TypesafeDecider(ISettings settings, ILogger logger)
		{
			this.settings = settings;
			this.logger = logger;
		}

		// Same home as every other key in plang: %Settings.TypeSafeApiKey%, kept in system.sqlite.
		// A missing key must never prompt: settings.Get throws MissingSettingsException, which the
		// engine turns into the ask-user flow, and that has no place inside a build. The miss is
		// swallowed here and Ask turns it into an error the builder falls back from.
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
			var (answers, error) = await Choose(stepText, new()
			{
				["module"] = new DeciderQuestion("This is one step of plang code. Which plang module implements what this step does?", modules)
			});
			if (error != null || answers == null || !answers.TryGetValue("module", out var answer)) return (null, error);

			return (new ModuleChoice(answer.Choice, answer.Confidence, answer.Probabilities), null);
		}

		public async Task<(MethodChoice? Choice, IError? Error)> ChooseMethod(string stepText, string module, Dictionary<string, string> methods)
		{
			var (answers, error) = await Choose(stepText, new()
			{
				["method"] = new DeciderQuestion($"This is one step of plang code that uses the module {module}. Which method of the module does this step call?", methods)
			});
			if (error != null || answers == null || !answers.TryGetValue("method", out var answer)) return (null, error);

			return (new MethodChoice(answer.Choice, answer.Confidence, answer.Probabilities), null);
		}

		public async Task<(Dictionary<string, ParameterChoice>? Choices, IError? Error)> ChooseParameters(string stepText, string method, Dictionary<string, ParameterQuestion> parameters)
		{
			var questions = new Dictionary<string, DeciderQuestion>();
			foreach (var parameter in parameters)
			{
				questions[parameter.Key] = new DeciderQuestion(
					$"This is one step of plang code that calls the method {method}. Parameter '{parameter.Key}': {parameter.Value.Description} Which of these is its value in this step?",
					parameter.Value.Candidates);
			}

			var (answers, error) = await Choose(stepText, questions);
			if (error != null || answers == null) return (null, error);

			var choices = new Dictionary<string, ParameterChoice>();
			foreach (var answer in answers)
			{
				choices[answer.Key] = new ParameterChoice(answer.Value.Choice, answer.Value.Confidence);
			}
			return (choices, null);
		}

		// One request, any number of Choice questions: the step is the state, each question's
		// options are its criteria, and each answer is the option key plus how sure the model is.
		// Nothing thrown in here may reach the builder: every failure comes back as an error that
		// names itself, and the builder falls back to the llm for the step.
		public async Task<(Dictionary<string, DeciderAnswer>? Answers, IError? Error)> Choose(string state, Dictionary<string, DeciderQuestion> questions)
		{
			if (questions.Count == 0)
			{
				return (null, new ServiceError("No questions to ask", this.GetType()));
			}
			foreach (var question in questions)
			{
				if (question.Value.Criteria == null || question.Value.Criteria.Count == 0)
				{
					return (null, new ServiceError($"No options to choose from for '{question.Key}'", this.GetType()));
				}
			}

			var key = GetKey();
			if (string.IsNullOrEmpty(key))
			{
				return (null, new ServiceError("TypeSafeApiKey is not set in this app's settings (%Settings.TypeSafeApiKey%)", this.GetType(), Key: "MissingTypeSafeApiKey"));
			}

			try
			{
				var body = new Dictionary<string, object>
				{
					["state"] = state,
					["model"] = Model,
					["questions"] = questions.ToDictionary(q => q.Key, q => (object)new Dictionary<string, object>
					{
						["type"] = "choice",
						["instructions"] = q.Value.Instructions,
						["criteria"] = q.Value.Criteria
					})
				};

				using var request = new HttpRequestMessage(HttpMethod.Post, Url);
				request.Headers.UserAgent.ParseAdd("plang v0.1");
				request.Headers.Add("Authorization", $"Bearer {key}");
				request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

				using var response = await httpClient.SendAsync(request);
				var responseBody = await response.Content.ReadAsStringAsync();
				if (!response.IsSuccessStatusCode)
				{
					return (null, new ServiceError($"Typesafe returned {(int)response.StatusCode}: {responseBody}", this.GetType(), StatusCode: (int)response.StatusCode));
				}

				var answersJson = JObject.Parse(responseBody)["answers"] as JObject;
				if (answersJson == null)
				{
					return (null, new ServiceError("Typesafe gave no answers: " + responseBody, this.GetType()));
				}

				var answers = new Dictionary<string, DeciderAnswer>();
				foreach (var question in questions)
				{
					var answer = answersJson[question.Key];
					var choice = answer?["choice"]?.ToString();
					if (string.IsNullOrEmpty(choice))
					{
						return (null, new ServiceError($"Typesafe gave no '{question.Key}' choice: {responseBody}", this.GetType()));
					}
					var confidence = answer?["confidence"]?.Value<double>() ?? 0;
					var probabilities = answer?["probabilities"]?.ToObject<Dictionary<string, double>>() ?? new();
					answers[question.Key] = new DeciderAnswer(choice, confidence, probabilities);
					logger.LogDebug("Typesafe chose {Choice} for {Question} with confidence {Confidence}", choice, question.Key, confidence);
				}
				return (answers, null);
			}
			catch (Exception ex)
			{
				return (null, new ServiceError($"Decider failed with {ex.GetType().Name}: {ex.Message}", this.GetType()));
			}
		}
	}
}
