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
			else if (question.llmResponseType == "json" || !string.IsNullOrEmpty(question.scheme))
			{
				extractor = new JsonExtractor();

				if (string.IsNullOrEmpty(question.scheme))
				{
					question.scheme = TypeHelper.GetJsonSchema(responseType);
				}
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

			systemMessage.Content.Add(new LlmContent(extractor.GetRequiredResponse(responseType)));
			return extractor;
		}
	}
}
