﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Errors.Runtime
{
	public class RetryStepError
	{
		public RetryStepError(int maxRetryCount = 3, IError? error = null) { }
	}
}