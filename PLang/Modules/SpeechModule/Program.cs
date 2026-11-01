using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Services.SettingsService;
using System.ComponentModel;
using System.Text;

namespace PLang.Modules.SpeechModule
{
	public record SpeechResult(string FilePath, string Format, int SampleRate, double DurationSeconds, long SizeInBytes, string Model, string Voice);

	[Description("Text to speech with Google Gemini. Turns text into a spoken audio file, Icelandic and many other languages. The api key is read from the app settings by the module, the step never carries it")]
	public class Program : BaseProgram
	{
		private const string DefaultModel = "gemini-2.5-flash-preview-tts";
		private const string DefaultVoice = "Kore";
		private const int SampleRate = 24000;

		public Program() : base() { }

		[Description("Converts text to speech and saves it as a wav file. language is an optional hint such as is, en, da. voice is a Gemini voice name such as Kore, Puck, Charon, Aoede. style is an optional direction such as \"warm and calm\"")]
		[Example("convert %text% to speech, language: is, save to audio.wav, write to %speech%",
			@"text=%text%, language=""is"", filePath=""audio.wav""")]
		[Example("text to speech %script%, voice: Puck, save to /audio/step1.wav",
			@"text=%script%, voice=""Puck"", filePath=""/audio/step1.wav""")]
		[Example("read %text% aloud, language: is, style: warm and calm, save to %path%, write to %speech%",
			@"text=%text%, language=""is"", style=""warm and calm"", filePath=%path%")]
		public async Task<(SpeechResult?, IError?)> TextToSpeech(string text, string filePath, string? language = null, string? voice = null, string? style = null, string? model = null, string? apiKey = null)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return (null, new ProgramError("Text is required for text to speech", goalStep, function));
			}
			if (string.IsNullOrWhiteSpace(filePath))
			{
				return (null, new ProgramError("A file path is required, Gemini returns the audio in the response and it has to be saved somewhere, e.g. audio.wav", goalStep, function));
			}
			if (!filePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
			{
				return (null, new ProgramError($"Only .wav output is supported, got {filePath}. Gemini returns raw PCM and there is no mp3 encoder in the runtime yet", goalStep, function));
			}

			// Goal level %Settings.GeminiKey% is stored by MemoryStack under the Settings class with a lower cased key.
			apiKey = string.IsNullOrEmpty(apiKey) ? settings.GetOrDefault<string>(typeof(Settings), "geminikey", "") : apiKey;
			if (string.IsNullOrEmpty(apiKey))
			{
				return (null, new ProgramError("Gemini key is missing. Set it in the app settings as GeminiKey (get a key at https://aistudio.google.com/apikey)", goalStep, function));
			}

			model = string.IsNullOrEmpty(model) ? DefaultModel : model;
			voice = string.IsNullOrEmpty(voice) ? DefaultVoice : voice;

			var spoken = BuildPrompt(text, language, style);
			var body = new JObject
			{
				["contents"] = new JArray { new JObject { ["parts"] = new JArray { new JObject { ["text"] = spoken } } } },
				["generationConfig"] = new JObject
				{
					["responseModalities"] = new JArray("AUDIO"),
					["speechConfig"] = new JObject
					{
						["voiceConfig"] = new JObject
						{
							["prebuiltVoiceConfig"] = new JObject { ["voiceName"] = voice }
						}
					}
				}
			};

			try
			{
				using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
				using var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent");
				request.Headers.Add("x-goog-api-key", apiKey);
				request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");

				using var response = await httpClient.SendAsync(request);
				var responseBody = await response.Content.ReadAsStringAsync();
				if (!response.IsSuccessStatusCode)
				{
					return (null, new ProgramError($"Gemini returned {(int)response.StatusCode}: {Explain(response.StatusCode, responseBody)}", goalStep, function));
				}

				var inline = JObject.Parse(responseBody).SelectToken("candidates[0].content.parts[0].inlineData");
				var data = inline?["data"]?.ToString();
				if (string.IsNullOrEmpty(data))
				{
					return (null, new ProgramError($"Gemini answered without audio. The model may have refused the text or answered with text only: {Shorten(responseBody)}", goalStep, function));
				}

				var rate = ParseRate(inline?["mimeType"]?.ToString());
				var pcm = Convert.FromBase64String(data);
				var wav = AddWavHeader(pcm, rate);

				var fullPath = GetPath(filePath);
				var directory = fileSystem.Path.GetDirectoryName(fullPath);
				if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
				{
					fileSystem.Directory.CreateDirectory(directory);
				}
				await fileSystem.File.WriteAllBytesAsync(fullPath, wav);

				return (new SpeechResult(fullPath, "wav", rate, Math.Round(pcm.Length / 2.0 / rate, 2), wav.Length, model, voice), null);
			}
			catch (TaskCanceledException ex)
			{
				return (null, new ProgramError("Gemini did not answer within 180 seconds", goalStep, function, Exception: ex));
			}
			catch (Exception ex)
			{
				return (null, new ProgramError($"Text to speech failed: {ex.Message}", goalStep, function, Exception: ex));
			}
		}

		// The tts models take no language parameter, they detect it from the text. A direction in front of the
		// text is how Gemini is told how to read it, so it is only added when the step asked for one.
		private static string BuildPrompt(string text, string? language, string? style)
		{
			var direction = new List<string>();
			if (!string.IsNullOrEmpty(language)) direction.Add($"in {LanguageName(language)}");
			if (!string.IsNullOrEmpty(style)) direction.Add(style);
			if (direction.Count == 0) return text;

			return $"Say {string.Join(", ", direction)}: {text}";
		}

		private static string LanguageName(string language)
		{
			return language.ToLowerInvariant() switch
			{
				"is" or "is-is" => "Icelandic",
				"en" or "en-us" or "en-gb" => "English",
				"da" => "Danish",
				"sv" => "Swedish",
				"nb" or "no" => "Norwegian",
				"de" => "German",
				"fr" => "French",
				"es" => "Spanish",
				_ => language
			};
		}

		// audio/L16;codec=pcm;rate=24000
		private static int ParseRate(string? mimeType)
		{
			if (string.IsNullOrEmpty(mimeType)) return SampleRate;

			var marker = "rate=";
			var index = mimeType.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
			if (index < 0) return SampleRate;

			var digits = new string(mimeType.Skip(index + marker.Length).TakeWhile(char.IsDigit).ToArray());
			return int.TryParse(digits, out var rate) && rate > 0 ? rate : SampleRate;
		}

		private static byte[] AddWavHeader(byte[] pcm, int sampleRate)
		{
			const short channels = 1;
			const short bitsPerSample = 16;
			var byteRate = sampleRate * channels * bitsPerSample / 8;

			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream);
			writer.Write(Encoding.ASCII.GetBytes("RIFF"));
			writer.Write(36 + pcm.Length);
			writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
			writer.Write(16);
			writer.Write((short)1);
			writer.Write(channels);
			writer.Write(sampleRate);
			writer.Write(byteRate);
			writer.Write((short)(channels * bitsPerSample / 8));
			writer.Write(bitsPerSample);
			writer.Write(Encoding.ASCII.GetBytes("data"));
			writer.Write(pcm.Length);
			writer.Write(pcm);
			writer.Flush();
			return stream.ToArray();
		}

		private static string Explain(System.Net.HttpStatusCode status, string responseBody)
		{
			var reason = (int)status switch
			{
				400 => "the request was refused, check the model and voice name",
				401 or 403 => "the key was refused, check the GeminiKey setting",
				404 => "the model was not found for this key",
				429 => "rate limited or out of quota",
				>= 500 => "Google is having trouble, it is worth retrying",
				_ => "no reason given"
			};
			return $"{reason}. {Shorten(responseBody)}";
		}

		private static string Shorten(string value)
		{
			return value.Length <= 400 ? value : value.Substring(0, 400) + "...";
		}
	}
}
