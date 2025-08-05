using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Modules.DbModule.Builder;

namespace PLang.Modules.FileModule
{
	public class Builder : BaseBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;

		public Builder(IPLangFileSystem fileSystem, ILogger logger)
		{
			this.fileSystem = fileSystem;
			this.logger = logger;
		}
		
		public async Task<IBuilderError?> BuilderReadExcelFile(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			return await BuilderReadTextFile(step, instruction, gf);
		}
		
		public async Task<IBuilderError?> BuilderReadJson(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			return await BuilderReadTextFile(step, instruction, gf);
		}

		public async Task<IBuilderError?> BuilderReadJsonLineFile(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			return await BuilderReadTextFile(step, instruction, gf);
		}
		public async Task<IBuilderError?> BuilderReadBinaryFileAndConvertToBase64(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			return await BuilderReadTextFile(step, instruction, gf);
		}
		public async Task<IBuilderError?> BuilderReadFileAsStream(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			return await BuilderReadTextFile(step, instruction, gf);
		}
		public async Task<IBuilderError?> BuilderReadTextFile(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			var path = gf.GetParameter<string>("path");
			if (path?.Contains("%") == true) return null;

			var absolutePath = PathHelper.GetPath(path, fileSystem, step.Goal);
			try
			{
				if (!fileSystem.File.Exists(absolutePath))
				{
					logger.LogWarning($"Could not find file: {path}. This may not be a problem, depending on your system. Looked for it at {absolutePath}.");
				}
			} catch (FileAccessException ex)
			{
				logger.LogWarning($"  - Dont have permission to read file {path}. Will ask for permission at runtime");
			}
			return null;
		}
		}
}
