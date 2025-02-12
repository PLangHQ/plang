using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PLang.Errors;
using PLang.Errors.Handlers;

namespace PLang.Exceptions
{
    public class MissingSettingsException : AskUserError
	{
		private readonly Type callingType;
		private readonly string type;
		private readonly string key;
		private readonly object defaultValue;

		public object DefaultValue { get { return defaultValue; } }	
		public MissingSettingsException(Type callingType, string type, string key, object defaultValue, string explain, Action<Type, string, string, object> callback) : base(explain, CreateAdapter(callback)) {
			this.callingType = callingType;
			this.type = type;
			this.key = key;
			this.defaultValue = defaultValue;
		}

		public override async Task<IError?> InvokeCallback(object value)
		{
			var task = Callback?.Invoke(new object[] { callingType, type, key, value });
			if (task == null) return null;
			
			return await task;
			
		}


	}
}
