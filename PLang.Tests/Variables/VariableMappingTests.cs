using PLang.Variables;
using PLang.Variables.Errors;
using Assert = TUnit.Assertions.Assert;

namespace PLang.Tests.Variables;

public class VariableMappingTests
{
	[Test]
	public async Task SimpleVariable_NoOperations_ParsesCorrectly()
	{
		// Arrange
		var input = "Hello %name%";
		var llmMapping = new VariableMapping
		{
			OriginalText = input,
			Variables = new List<LlmVariable>
			{
				new LlmVariable
				{
					FullExpression = "%name%",
					VariableName = "name",
					Operations = new List<Operation>()
				}
			}
		};

		var helper = new VariableMappingHelper();

		// Act
		var (result, error) = helper.ValidateMapping(llmMapping);

		// Assert
		await Assert.That(error).IsNull();
		await Assert.That(result).IsNotNull();
		await Assert.That(result.Variables.Count).IsEqualTo(1);
		await Assert.That(result.Variables[0].VariableName).IsEqualTo("name");
		await Assert.That(result.Variables[0].Start).IsEqualTo(6);
		await Assert.That(result.Variables[0].End).IsEqualTo(12);
	}

	[Test]
	public async Task MultipleVariables_ParsesCorrectly()
	{
		// Arrange
		var input = "Hello %name% - %address%";
		var llmMapping = new VariableMapping
		{
			OriginalText = input,
			Variables = new List<LlmVariable>
			{
				new LlmVariable
				{
					FullExpression = "%name%",
					VariableName = "name",
					Operations = new List<Operation>()
				},
				new LlmVariable
				{
					FullExpression = "%address%",
					VariableName = "address",
					Operations = new List<Operation>()
				}
			}
		};

		var helper = new VariableMappingHelper();

		// Act
		var (result, error) = helper.ValidateMapping(llmMapping);

		// Assert
		await Assert.That(error).IsNull();
		await Assert.That(result).IsNotNull();
		await Assert.That(result.Variables.Count).IsEqualTo(2);
		await Assert.That(result.Variables[0].Start).IsEqualTo(6);
		await Assert.That(result.Variables[0].End).IsEqualTo(12);
		await Assert.That(result.Variables[1].Start).IsEqualTo(15);
		await Assert.That(result.Variables[1].End).IsEqualTo(24);
	}

	[Test]
	public async Task VariableWithToUpper_ParsesCorrectly()
	{
		// Arrange
		var input = "Hello %name | to upper%";
		var llmMapping = new VariableMapping
		{
			OriginalText = input,
			Variables = new List<LlmVariable>
			{
				new LlmVariable
				{
					FullExpression = "%name | to upper%",
					VariableName = "name",
					Operations = new List<Operation>
					{
						new Operation
						{
							Class = "string",
							Method = "ToUpper",
							Parameters = new object[] { },
							ReturnType = "string"
						}
					}
				}
			}
		};

		var helper = new VariableMappingHelper();

		// Act
		var (result, error) = helper.ValidateMapping(llmMapping);

		// Assert
		await Assert.That(error).IsNull();
		await Assert.That(result).IsNotNull();
		await Assert.That(result.Variables[0].Operations.Count).IsEqualTo(1);
		await Assert.That(result.Variables[0].Operations[0].Method).IsEqualTo("ToUpper");
	}

	[Test]
	public async Task PropertyAccess_ParsesCorrectly()
	{
		// Arrange
		var input = "User: %user.name%";
		var llmMapping = new VariableMapping
		{
			OriginalText = input,
			Variables = new List<LlmVariable>
			{
				new LlmVariable
				{
					FullExpression = "%user.name%",
					VariableName = "user",
					Operations = new List<Operation>
					{
						new Operation
						{
							Class = "object",
							Method = "Column",
							Parameters = new object[] { "name" },
							ReturnType = "string"
						}
					}
				}
			}
		};

		var helper = new VariableMappingHelper();

		// Act
		var (result, error) = helper.ValidateMapping(llmMapping);

		// Assert
		await Assert.That(error).IsNull();
		await Assert.That(result).IsNotNull();
		await Assert.That(result.Variables[0].Operations.Count).IsEqualTo(1);
		await Assert.That(result.Variables[0].Operations[0].Method).IsEqualTo("Column");
		await Assert.That(result.Variables[0].Operations[0].Parameters[0]).IsEqualTo("name");
	}

	[Test]
	public async Task ArrayIndexing_ParsesCorrectly()
	{
		// Arrange
		var input = "Item: %products[0]%";
		var llmMapping = new VariableMapping
		{
			OriginalText = input,
			Variables = new List<LlmVariable>
			{
				new LlmVariable
				{
					FullExpression = "%products[0]%",
					VariableName = "products",
					Operations = new List<Operation>
					{
						new Operation
						{
							Class = "object",
							Method = "Index",
							Parameters = new object[] { 0 },
							ReturnType = "object"
						}
					}
				}
			}
		};

		var helper = new VariableMappingHelper();

		// Act
		var (result, error) = helper.ValidateMapping(llmMapping);

		// Assert
		await Assert.That(error).IsNull();
		await Assert.That(result).IsNotNull();
		await Assert.That(result.Variables[0].Operations.Count).IsEqualTo(1);
		await Assert.That(result.Variables[0].Operations[0].Method).IsEqualTo("Index");
		await Assert.That(result.Variables[0].Operations[0].Parameters[0]).IsEqualTo(0);
	}

	[Test]
	public async Task ComplexChaining_ParsesCorrectly()
	{
		// Arrange
		var input = "Name: %user.firstName | to upper | split(' ')[0]%";
		var llmMapping = new VariableMapping
		{
			OriginalText = input,
			Variables = new List<LlmVariable>
			{
				new LlmVariable
				{
					FullExpression = "%user.firstName | to upper | split(' ')[0]%",
					VariableName = "user",
					Operations = new List<Operation>
					{
						new Operation
						{
							Class = "object",
							Method = "Column",
							Parameters = new object[] { "firstName" },
							ReturnType = "string"
						},
						new Operation
						{
							Class = "string",
							Method = "ToUpper",
							Parameters = new object[] { },
							ReturnType = "string"
						},
						new Operation
						{
							Class = "string",
							Method = "Split",
							Parameters = new object[] { " " },
							ReturnType = "string[]"
						},
						new Operation
						{
							Class = "object",
							Method = "Index",
							Parameters = new object[] { 0 },
							ReturnType = "string"
						}
					}
				}
			}
		};

		var helper = new VariableMappingHelper();

		// Act
		var (result, error) = helper.ValidateMapping(llmMapping);

		// Assert
		await Assert.That(error).IsNull();
		await Assert.That(result).IsNotNull();
		await Assert.That(result.Variables[0].Operations.Count).IsEqualTo(4);
		await Assert.That(result.Variables[0].Operations[0].Method).IsEqualTo("Column");
		await Assert.That(result.Variables[0].Operations[1].Method).IsEqualTo("ToUpper");
		await Assert.That(result.Variables[0].Operations[2].Method).IsEqualTo("Split");
		await Assert.That(result.Variables[0].Operations[3].Method).IsEqualTo("Index");
	}

	[Test]
	public async Task ArithmeticOperation_Multiply_ParsesCorrectly()
	{
		// Arrange
		var input = "Price: %price * 5%%";
		var llmMapping = new VariableMapping
		{
			OriginalText = input,
			Variables = new List<LlmVariable>
			{
				new LlmVariable
				{
					FullExpression = "%price * 5%%",
					VariableName = "price",
					Operations = new List<Operation>
					{
						new Operation
						{
							Class = "decimal",
							Method = "Multiply",
							Parameters = new object[] { "5%" },
							ReturnType = "decimal"
						}
					}
				}
			}
		};

		var helper = new VariableMappingHelper();

		// Act
		var (result, error) = helper.ValidateMapping(llmMapping);

		// Assert
		await Assert.That(error).IsNull();
		await Assert.That(result).IsNotNull();
		await Assert.That(result.Variables[0].Operations.Count).IsEqualTo(1);
		await Assert.That(result.Variables[0].Operations[0].Method).IsEqualTo("Multiply");
		await Assert.That(result.Variables[0].Operations[0].Parameters[0]).IsEqualTo("5%");
	}

	[Test]
	public async Task VariableNotFound_ReturnsError()
	{
		// Arrange
		var input = "Hello world";
		var llmMapping = new VariableMapping
		{
			OriginalText = input,
			Variables = new List<LlmVariable>
			{
				new LlmVariable
				{
					FullExpression = "%name%",
					VariableName = "name",
					Operations = new List<Operation>()
				}
			}
		};

		var helper = new VariableMappingHelper();

		// Act
		var (result, error) = helper.ValidateMapping(llmMapping);

		// Assert
		await Assert.That(error).IsNotNull();
		await Assert.That(error).IsTypeOf<VariableNotFoundError>();
		await Assert.That(error.Message).Contains("%name%");
	}

	[Test]
	public async Task InvalidClass_ReturnsError()
	{
		// Arrange
		var input = "Hello %name%";
		var llmMapping = new VariableMapping
		{
			OriginalText = input,
			Variables = new List<LlmVariable>
			{
				new LlmVariable
				{
					FullExpression = "%name%",
					VariableName = "name",
					Operations = new List<Operation>
					{
						new Operation
						{
							Class = "NonExistentClass",
							Method = "SomeMethod",
							Parameters = new object[] { },
							ReturnType = "string"
						}
					}
				}
			}
		};

		var helper = new VariableMappingHelper();

		// Act
		var (result, error) = helper.ValidateMapping(llmMapping);

		// Assert
		await Assert.That(error).IsNotNull();
		await Assert.That(error).IsTypeOf<ClassNotFoundError>();
		await Assert.That(error.Message).Contains("NonExistentClass");
	}
}

public class VariableExecutionTests
{
	[Test]
	public async Task SimpleVariable_ExecutesCorrectly()
	{
		// Arrange
		var variables = new Dictionary<string, object>
		{
			["name"] = "John Doe"
		};

		var runtimeVariable = new RuntimeVariable
		{
			FullExpression = "%name%",
			VariableName = "name",
			Start = 6,
			End = 12,
			Operations = new List<RuntimeOperation>()
		};

		var executor = new PipeExecutor(variables);

		// Act
		var result = executor.Execute("name", new PipelineResult { Operations = runtimeVariable.Operations });

		// Assert
		await Assert.That(result).IsEqualTo("John Doe");
	}

	[Test]
	public async Task ToUpper_ExecutesCorrectly()
	{
		// Arrange
		var variables = new Dictionary<string, object>
		{
			["name"] = "john doe"
		};

		var pipeline = new PipelineResult
		{
			Operations = new List<RuntimeOperation>
			{
				new RuntimeOperation
				{
					Class = "string",
					Method = "ToUpper",
					Parameters = new object[] { },
					ReturnType = "string"
				}
			}
		};

		var executor = new PipeExecutor(variables);

		// Act
		var result = executor.Execute("name", pipeline);

		// Assert
		await Assert.That(result).IsEqualTo("JOHN DOE");
	}

	[Test]
	public async Task PropertyAccess_ExecutesCorrectly()
	{
		// Arrange
		var variables = new Dictionary<string, object>
		{
			["user"] = new { Name = "Alice", Age = 30 }
		};

		var pipeline = new PipelineResult
		{
			Operations = new List<RuntimeOperation>
			{
				new RuntimeOperation
				{
					Class = "object",
					Method = "Column",
					Parameters = new object[] { "Name" },
					ReturnType = "string"
				}
			}
		};

		var executor = new PipeExecutor(variables);

		// Act
		var result = executor.Execute("user", pipeline);

		// Assert
		await Assert.That(result).IsEqualTo("Alice");
	}

	[Test]
	public async Task ArrayIndexing_ExecutesCorrectly()
	{
		// Arrange
		var variables = new Dictionary<string, object>
		{
			["numbers"] = new[] { 10, 20, 30, 40 }
		};

		var pipeline = new PipelineResult
		{
			Operations = new List<RuntimeOperation>
			{
				new RuntimeOperation
				{
					Class = "object",
					Method = "Index",
					Parameters = new object[] { 2 },
					ReturnType = "int"
				}
			}
		};

		var executor = new PipeExecutor(variables);

		// Act
		var result = executor.Execute("numbers", pipeline);

		// Assert
		await Assert.That(result).IsEqualTo(30);
	}

	[Test]
	public async Task ChainedOperations_ExecutesCorrectly()
	{
		// Arrange
		var variables = new Dictionary<string, object>
		{
			["text"] = "hello world from plang"
		};

		var pipeline = new PipelineResult
		{
			Operations = new List<RuntimeOperation>
			{
				new RuntimeOperation
				{
					Class = "string",
					Method = "ToUpper",
					Parameters = new object[] { },
					ReturnType = "string"
				},
				new RuntimeOperation
				{
					Class = "string",
					Method = "Split",
					Parameters = new object[] { " " },
					ReturnType = "string[]"
				},
				new RuntimeOperation
				{
					Class = "object",
					Method = "Index",
					Parameters = new object[] { 0 },
					ReturnType = "string"
				}
			}
		};

		var executor = new PipeExecutor(variables);

		// Act
		var result = executor.Execute("text", pipeline);

		// Assert
		await Assert.That(result).IsEqualTo("HELLO");
	}

	[Test]
	public async Task MultiplyByPercentage_ExecutesCorrectly()
	{
		// Arrange
		var variables = new Dictionary<string, object>
		{
			["price"] = 100m
		};

		var pipeline = new PipelineResult
		{
			Operations = new List<RuntimeOperation>
			{
				new RuntimeOperation
				{
					Class = "decimal",
					Method = "Multiply",
					Parameters = new object[] { "5%" },
					ReturnType = "decimal"
				}
			}
		};

		var executor = new PipeExecutor(variables);

		// Act
		var result = executor.Execute("price", pipeline);

		// Assert
		await Assert.That(result).IsEqualTo(5m);
	}

	[Test]
	public async Task NestedPropertyAccess_ExecutesCorrectly()
	{
		// Arrange
		var variables = new Dictionary<string, object>
		{
			["user"] = new
			{
				Name = "Bob",
				Address = new { City = "Reykjavik", Zip = "101" }
			}
		};

		var pipeline = new PipelineResult
		{
			Operations = new List<RuntimeOperation>
			{
				new RuntimeOperation
				{
					Class = "object",
					Method = "Column",
					Parameters = new object[] { "Address" },
					ReturnType = "object"
				},
				new RuntimeOperation
				{
					Class = "object",
					Method = "Column",
					Parameters = new object[] { "City" },
					ReturnType = "string"
				}
			}
		};

		var executor = new PipeExecutor(variables);

		// Act
		var result = executor.Execute("user", pipeline);

		// Assert
		await Assert.That(result).IsEqualTo("Reykjavik");
	}
}

public class TemplateExecutionTests
{
	[Test]
	public async Task SimpleTemplate_ExecutesCorrectly()
	{
		// Arrange
		var variables = new Dictionary<string, object>
		{
			["name"] = "Alice"
		};

		var template = new RuntimeVariableMapping
		{
			OriginalText = "Hello %name%!",
			Variables = new List<RuntimeVariable>
			{
				new RuntimeVariable
				{
					FullExpression = "%name%",
					VariableName = "name",
					Start = 6,
					End = 12,
					Operations = new List<RuntimeOperation>()
				}
			}
		};

		var executor = new FastTemplateExecutor(variables);

		// Act
		var result = executor.Execute(template);

		// Assert
		await Assert.That(result).IsEqualTo("Hello Alice!");
	}

	[Test]
	public async Task TemplateWithOperations_ExecutesCorrectly()
	{
		// Arrange
		var variables = new Dictionary<string, object>
		{
			["name"] = "alice"
		};

		var template = new RuntimeVariableMapping
		{
			OriginalText = "Hello %name | to upper%!",
			Variables = new List<RuntimeVariable>
			{
				new RuntimeVariable
				{
					FullExpression = "%name | to upper%",
					VariableName = "name",
					Start = 6,
					End = 23,
					Operations = new List<RuntimeOperation>
					{
						new RuntimeOperation
						{
							Class = "string",
							Method = "ToUpper",
							Parameters = new object[] { },
							ReturnType = "string"
						}
					}
				}
			}
		};

		var executor = new FastTemplateExecutor(variables);

		// Act
		var result = executor.Execute(template);

		// Assert
		await Assert.That(result).IsEqualTo("Hello ALICE!");
	}

	[Test]
	public async Task MultipleVariablesInTemplate_ExecutesCorrectly()
	{
		// Arrange
		var variables = new Dictionary<string, object>
		{
			["name"] = "Bob",
			["city"] = "Reykjavik"
		};

		var template = new RuntimeVariableMapping
		{
			OriginalText = "%name% lives in %city%",
			Variables = new List<RuntimeVariable>
			{
				new RuntimeVariable
				{
					FullExpression = "%name%",
					VariableName = "name",
					Start = 0,
					End = 6,
					Operations = new List<RuntimeOperation>()
				},
				new RuntimeVariable
				{
					FullExpression = "%city%",
					VariableName = "city",
					Start = 16,
					End = 22,
					Operations = new List<RuntimeOperation>()
				}
			}
		};

		var executor = new FastTemplateExecutor(variables);

		// Act
		var result = executor.Execute(template);

		// Assert
		await Assert.That(result).IsEqualTo("Bob lives in Reykjavik");
	}
}