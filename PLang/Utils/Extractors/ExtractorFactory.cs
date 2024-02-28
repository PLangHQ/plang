using PLang.Models;
using PLang.Modules.UiModule;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils.Extractors
{
	public class ExtractorFactory
	{
		public static IContentExtractor GetExtractor(LlmRequest question, Type responseType)
		{
			if (string.IsNullOrEmpty(question.llmResponseType) && JsonHelper.LookAsJsonScheme(question.scheme))
			{
				question.llmResponseType = "json";
			}

			string? requiredResponse = null;
			IContentExtractor extractor;
			if (question.llmResponseType == "text")
			{
				extractor = new TextExtractor();
			}
			else if (responseType == typeof(UiResponse))
			{
				question.llmResponseType = "html";
				extractor = new HtmlExtractor();
			}
			else if (question.llmResponseType == "csharp")
			{
				extractor = new CSharpExtractor();
			}
			else if (question.llmResponseType == "json")
			{
				extractor = new JsonExtractor();

				if (string.IsNullOrEmpty(question.scheme))
				{
					question.scheme = TypeHelper.GetJsonSchema(responseType);
				}
				requiredResponse = ((JsonExtractor) extractor).GetRequiredResponse(question.scheme);
			}
			else
			{
				extractor = new GenericExtractor(question.llmResponseType);
			}

			var systemMessage = question.promptMessage.FirstOrDefault(p => p.Role == "system");
			if (systemMessage == null)
			{
				systemMessage = new LlmMessage() { Role = "system", Content = new() };
			}

			if (requiredResponse == null)
			{
				requiredResponse = extractor.GetRequiredResponse(responseType);
			}
			systemMessage.Content.Add(new LlmContent(requiredResponse));
			return extractor;
		}
	}
}
