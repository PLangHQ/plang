namespace PLang.Variables;

using System.Collections.Generic;

public class PipelineResult
{
	public List<RuntimeOperation> Operations { get; set; } = new List<RuntimeOperation>();
}