Step text: `system: analyze sentiment, user: %comment%, schema: {sentiment: string}, write to %result%`
Mapping: `llm.query Messages([list<LlmMessage>] [{"Role":"system","Content":"analyze sentiment"},{"Role":"user","Content":"%comment%"}]), Schema([string] {sentiment: string}) | variable.set Name([string] %result%), Value([object] %!data%)`
