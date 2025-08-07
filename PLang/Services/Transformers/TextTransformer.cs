using NBitcoin;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Runtime;
using PLang.Services.Transformers;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static PLang.Modules.OutputModule.Program;
using static PLang.Utils.StepHelper;

namespace PLang.Services.Transformers
{


	public class TextTransformer : ITransformer
	{
		private readonly Encoding encoding;

		public TextTransformer(Encoding encoding)
		{
			this.encoding = encoding;
		}
		public Encoding Encoding { get { return encoding; } }

		public virtual string ContentType { get { return "plain/text"; } }
		public (object?, IError?) Transform(object obj, Dictionary<string, object?>? properties = null, string type = "text")
		{
			if (obj == null) return (null, null);
			if (obj is string) return (obj, null);
			try
			{
				string content = obj.ToString() ?? string.Empty;
				var fullName = obj.GetType().FullName ?? "";
				if (fullName.IndexOf("[") != -1)
				{
					fullName = fullName.Substring(0, fullName.IndexOf("["));
				}

				if (content != null && !content.StartsWith(fullName))
				{
					if (!TypeHelper.IsConsideredPrimitive(obj.GetType()) && !TypeHelper.IsRecordWithToString(obj))
					{
						return (JsonConvert.SerializeObject(obj, Formatting.Indented), null);
					}
					else
					{
						return (content, null);
					}
				}

				return (JsonConvert.SerializeObject(obj, Formatting.Indented), null);

			}
			catch (Exception ex)
			{
				return (null, new ServiceError(ex.Message, GetType(), "TransformError", 500, Exception: ex));
			}
		}

		public async Task<IError?> Transform(Stream stream, object obj, Dictionary<string, object?>? properties = null, string type = "text")
		{
			if (obj == null) return null;
			try
			{
				byte[]? bytes = null;
				if (obj is string str)
				{
					bytes = encoding.GetBytes(str);
				}
				else
				{
					string content = obj.ToString() ?? string.Empty;
					var fullName = obj.GetType().FullName ?? "";
					if (fullName.IndexOf("[") != -1)
					{
						fullName = fullName.Substring(0, fullName.IndexOf("["));
					}


					if (content != null && !content.StartsWith(fullName))
					{
						if (!TypeHelper.IsConsideredPrimitive(obj.GetType()) && !TypeHelper.IsRecordWithToString(obj))
						{
							bytes = encoding.GetBytes(JsonConvert.SerializeObject(obj, Formatting.Indented));
						}
						else
						{
							bytes = encoding.GetBytes(content);
						}
					}
					else
					{
						bytes = encoding.GetBytes(JsonConvert.SerializeObject(obj, Formatting.Indented));
					}
				}

				await stream.WriteAsync(bytes);
				await stream.FlushAsync();

				return null;
			}
			catch (Exception ex)
			{
				return new ServiceError(ex.Message, GetType(), "TransformError", 500, Exception: ex);
			}
		}
	}
}
