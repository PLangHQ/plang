Step text: `system: analyze sentiment, user: %comment%, schema: {sentiment: string}, write to %result%`
Properties: `{"Message": [{"Role": "system", "Content": "analyze sentiment"}, {"Role": "user", "Content": "%comment%"}], "Schema": {"sentiment": "string"}}`

Step text: `ask the llm "summarise %text%", write to %summary%`
Properties: `{"Message": [{"Role": "user", "Content": "summarise %text%"}]}` — a step with no system part is one user message.
