using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using NCalc;

namespace PLang.Models.ObjectValueExtractors
{
	public class MathExpressionExtractor : IExtractor
	{
		private readonly string expression;
		private readonly ObjectValue parent;

		public MathExpressionExtractor(string expression, ObjectValue parent)
		{
			this.expression = expression;
			this.parent = parent;
		}

		public ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null)
		{
			var expressionObj = new NCalc.Expression(parent.Value + expression.Replace(",", "."));
			var result = expressionObj.Evaluate();
			

			return new ObjectValue(segment.Value, result);
		}
	}
}
