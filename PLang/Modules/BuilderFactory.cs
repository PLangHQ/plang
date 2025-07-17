using LightInject;
using Org.BouncyCastle.Crypto.Tls;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Modules
{
	public interface IBuilderFactory
	{
		BaseBuilder Create(string builderName);
	}

	public class BuilderFactory : IBuilderFactory
	{
		private readonly ServiceContainer _container;
		private readonly ITypeHelper typeHelper;
		private readonly ILogger logger;

		public BuilderFactory(ServiceContainer container, ITypeHelper typeHelper, ILogger logger)
		{
			_container = container;
			this.typeHelper = typeHelper;
			this.logger = logger;
		}

		public BaseBuilder Create(string builderName)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			logger.LogDebug($"      -- GetBuilderType: {stopwatch.ElapsedMilliseconds}");
			// Use reflection to get the type
			var type = typeHelper.GetBuilderType(builderName);
			if (type == null)
			{
				type = typeof(GenericFunctionBuilder);
			}

			logger.LogDebug($"      -- Create Instance: {stopwatch.ElapsedMilliseconds}");
			// Use the container to resolve the instance
			string fullName = type.FullName;

			var instance = (BaseBuilder)_container.GetInstance(type);
			logger.LogDebug($"      -- Have instance: {stopwatch.ElapsedMilliseconds}");
			return instance;
		}
	}

}
