

namespace PLang.Exceptions
{
	public class SettingsException : Exception {
		public string Key;
		public string Explain;
		public object DefaultValue;

		public SettingsException(string key, object defaultValue, string explain)
		{
			this.Key = key;
			this.Explain = explain;
			this.DefaultValue = defaultValue;
		}
	}
	public class InvalidSettingsException : SettingsException
	{
		public InvalidSettingsException(string key, object defaultValue, string explain) : base(key, defaultValue, explain) { }


	}
}


