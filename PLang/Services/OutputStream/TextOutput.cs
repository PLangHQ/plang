using PLang.Errors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Services.OutputStream
{
	public interface IOutput {
		object? Data { get; set; }
	}
	public record TextOutput(object? Data, string ContentType, bool Cache, IError ErrorDetail, string Channel) : IOutput
	{
		public object? Data { get; set; } = Data;
	}
}
