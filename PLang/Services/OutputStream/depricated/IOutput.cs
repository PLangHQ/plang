using Newtonsoft.Json;
using PLang.Errors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Services.OutputStream.depricated;
/*
public interface IOutput<T> where T : IData
{
	T Data { get; set; }
}

public interface IData
{
	object Data { get; set; }
	string ContentType { get; set; }
	bool Cache { get; set; }
	string Error => FormatException("text", ErrorDetail);
	IError ErrorDetail { get; set; }
	string Channel { get; set; }

	// Default implementation for Error
	private string FormatException(string type, IError ErrorDetail)
	{
		if (type == "json")
		{
			return JsonConvert.SerializeObject(Data);
		}
		return Error.ToString() ?? "Error is empty";
	}
}
*/