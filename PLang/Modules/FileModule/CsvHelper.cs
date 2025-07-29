using CsvHelper;
using CsvHelper.Configuration;
using Markdig.Helpers;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Models;
using PLang.Modules.DbModule;
using PLang.Utils;
using System.Collections;
using System.Globalization;

namespace PLang.Modules.FileModule
{
	public class CsvHelper
	{
		public record CsvOptions(string Delimiter = ",", bool HasHeaderRecord = true, string NewLine = "\n", 
			string Encoding = "utf-8", bool IgnoreBlankLines = true,
			bool AllowComments = false, char Comment = '#', GoalToCallInfo? GoalToCallOnBadData = null);
		public static async Task<IError?> WriteToStream(TextWriter textWriter, object obj, CsvOptions options)
		{

			IWriterConfiguration writeConfig = new CsvConfiguration(CultureInfo.CurrentCulture)
			{
				Delimiter = options.Delimiter,
				BadDataFound = data =>
				{
					if (options.GoalToCallOnBadData == null) return;

					options.GoalToCallOnBadData.Parameters.Add("data", data);
					// todo: FIX, should be called here
					//pseudoRuntime.RunGoal(engine, context, fileSystem.RelativeAppPath, goalToCallOnBadData, Goal);
					throw new NotImplementedException();
				},
				NewLine = options.NewLine,
				Encoding = FileHelper.GetEncoding(options.Encoding),
				AllowComments = options.AllowComments,
				Comment = options.Comment,
				IgnoreBlankLines = options.IgnoreBlankLines,
				HasHeaderRecord = options.HasHeaderRecord,
				DetectColumnCountChanges = true,
				IgnoreReferences = false
			};

			if (obj is JArray jArray)
			{
				obj = jArray.ToList();
			}
			if (obj is JObject jObject)
			{
				obj = jObject.ToDictionary();
			}


			using var csv = new CsvWriter(textWriter, writeConfig, leaveOpen: true);

			if (obj is Table table)
			{
				foreach (var h in table[0].Columns)
					csv.WriteField(h);
				await csv.NextRecordAsync();

				foreach (var row in table)
				{
					foreach (var h in row)
						csv.WriteField(row.TryGetValue(h.Key, out var v) ? v : null);
					await csv.NextRecordAsync();
				}
				await csv.FlushAsync();
				return null;
			}


			if (obj is IEnumerable enumer)
			{



				var ble = obj as List<Dictionary<string, object?>>;
				if (ble == null)
				{
					await csv.WriteRecordsAsync(enumer);
					await csv.FlushAsync();
					return null;
				}

				foreach (var record in ble)
				{
					foreach (var key in record.Keys)
					{
						csv.WriteField(key);
					}
					csv.NextRecord();
					break;
				}

				foreach (var record in ble)
				{
					foreach (var value in record.Values)
					{
						csv.WriteField(value);
					}
					csv.NextRecord();
				}
			}
			else
			{
				csv.WriteRecord(obj);
			}
			await csv.FlushAsync(); 
			return null;
		}
	}
}
